using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Infrastructure.Persistence;
using System.Text.Json;
using System.Globalization;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;

namespace BetsTrading.Infrastructure.Services;

public class UpdaterService : IUpdaterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;
    private readonly AppDbContext _dbContext;
    private readonly ITickerMaxOddsService _tickerMaxOddsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly int[] Timeframes = { 1, 2, 4, 24 };
    private static readonly string[] TWELVE_DATA_KEYS = Enumerable.Range(0, 11)
        .Select(i => Environment.GetEnvironmentVariable($"TWELVE_DATA_KEY{i}") ?? "")
        .ToArray();
    private const decimal FIXED_EUR_USD = 1.16m;
    /// <summary>Mínimo 3 bandas por región (debajo / spot / encima) → 9 zonas por período.</summary>
    private const int ZonesPerPeriod = 9;
    private const double MaxTargetOddsCap = 999.99;

    /// <summary>Multiplicador de odds casi proporcional al timeframe (1h→1×, 2h→2×, 4h→4×, 24h→24×).</summary>
    private static double TimeframeOddsMultiplier(int timeframeHours) =>
        timeframeHours <= 0 ? 1.0 : timeframeHours;

    private static double ApplyTimeframeOddsBoost(double baseOdds, int timeframeHours)
    {
        double boosted = baseOdds * TimeframeOddsMultiplier(timeframeHours);
        return Math.Min(MaxTargetOddsCap, Math.Max(1.01, Math.Round(boosted, 2)));
    }

    public UpdaterService(
        IUnitOfWork unitOfWork,
        IApplicationLogger logger,
        AppDbContext dbContext,
        ITickerMaxOddsService tickerMaxOddsService,
        IHttpClientFactory httpClientFactory)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _dbContext = dbContext;
        _tickerMaxOddsService = tickerMaxOddsService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task UpdateAssetsAsync(bool marketHours, CancellationToken cancellationToken = default)
    {
        _logger.Debug("[UpdaterService] :: UpdateAssetsAsync called (MarketHours: {0})", marketHours);

        if (TWELVE_DATA_KEYS == null || TWELVE_DATA_KEYS.Length == 0 || TWELVE_DATA_KEYS.All(string.IsNullOrWhiteSpace))
        {
            _logger.Debug("[UpdaterService] :: TWELVE DATA KEYS not set in environment variables!");
            return;
        }

        // Obtener activos según el modo
        var allAssets = await _unitOfWork.FinancialAssets.GetAllAsync(cancellationToken);
        var assetsQuery = !marketHours
            ? allAssets.Where(a => a.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase) || 
                                  a.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
            : allAssets;

        var selectedAssets = assetsQuery.ToList();

        if (selectedAssets.Count == 0)
        {
            _logger.Debug("[UpdaterService] :: ZERO assets found! CHECK DATABASE");
            return;
        }

        // Obtener tipo de cambio EUR/USD
        var eurUsdCandle = await _unitOfWork.AssetCandles
            .GetLatestCandleAsync(223, "1h", cancellationToken); // EURUSD FOREX

        var eurToUsd = eurUsdCandle != null ? (decimal)eurUsdCandle.Close : FIXED_EUR_USD;

        var httpClient = _httpClientFactory.CreateClient("TwelveData");

        const string interval = "1h";
        const int latestCandlesCount = 10;
        const string desiredQuote = "EUR";

        int keyIndex = 0;
        int callsWithThisKey = 0;
        int totalCandlesSaved = 0;

        string CurrentKey() => TWELVE_DATA_KEYS[keyIndex] ?? string.Empty;

        void NextKeyLocal()
        {
            keyIndex = (keyIndex + 1) % TWELVE_DATA_KEYS.Length;
            callsWithThisKey = 0;
            _logger.Debug("[UpdaterService] :: Switching to next TwelveDataKey (index {0})", keyIndex);
            if (string.IsNullOrWhiteSpace(CurrentKey()))
                _logger.Debug("[UpdaterService] :: Current TwelveDataKey is EMPTY, check environment variables!");
        }

        foreach (var asset in selectedAssets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Rate limiting: rotar key después de 8 llamadas
            if (callsWithThisKey == 8)
            {
                var wrapped = (keyIndex + 1) % TWELVE_DATA_KEYS.Length == 0;
                NextKeyLocal();
                if (wrapped)
                {
                    _logger.Debug("[UpdaterService] :: Sleeping 45 seconds to bypass rate limit");
                    await Task.Delay(TimeSpan.FromSeconds(45), cancellationToken);
                }
            }

            var symbol = (asset.Ticker ?? string.Empty).Split('.')[0].Trim();
            if (string.IsNullOrWhiteSpace(symbol))
            {
                _logger.Debug("[UpdaterService] :: Asset {0} has empty ticker, skipping", asset.Id);
                continue;
            }

            // Siempre pedir las últimas N velas; la BD evita duplicados con BulkInsertOrUpdate (clave AssetId, Exchange, Interval, DateTime)
            string baseUrl = asset.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase)
                ? $"https://api.twelvedata.com/time_series?symbol={symbol}/{desiredQuote}&interval={interval}&timezone=UTC&apikey={CurrentKey()}"
                : $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={interval}&timezone=UTC&apikey={CurrentKey()}";
            string url = $"{baseUrl}&outputsize={latestCandlesCount}";

            // Llamar a la API
            HttpResponseMessage resp;
            try
            {
                resp = await httpClient.GetAsync(url, cancellationToken);
                callsWithThisKey++;
            }
            catch (Exception ex)
            {
                _logger.Debug("[UpdaterService] :: HTTP error fetching {0}: {1}", symbol, ex.Message);
                callsWithThisKey++;
                continue;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _logger.Debug("[UpdaterService] :: TwelveData: Failed for {0}. HTTP {1}", symbol, resp.StatusCode);
                continue;
            }

            string json;
            try
            {
                json = await resp.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Debug("[UpdaterService] :: Error reading content for {0}: {1}", symbol, ex.Message);
                continue;
            }

            // Parsear JSON
            TwelveDataResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<TwelveDataResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.Debug("[UpdaterService] :: JSON parse error for {0}: {1}", symbol, ex.Message);
                continue;
            }

            if (parsed == null || parsed.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.Debug("[UpdaterService] :: API status not ok for {0}. Raw: {1}", symbol, json);
                continue;
            }

            if (parsed.Values == null || parsed.Values.Count == 0)
            {
                _logger.Debug("[UpdaterService] :: No market data for {0}", symbol);
                continue;
            }

            var exchange = parsed.Meta?.Exchange ?? "Unknown";
            var newCandles = new List<AssetCandle>();

            // Procesar candles (BulkInsertOrUpdate evita duplicados por AssetId, Exchange, Interval, DateTime)
            foreach (var v in parsed.Values)
            {
                try
                {
                    // Twelve Data devuelve datetimes en UTC (timezone=UTC en la request); interpretar como UTC, no como hora local
                    var dtRaw = DateTime.Parse(v.Datetime!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    var dt = new DateTime(dtRaw.Year, dtRaw.Month, dtRaw.Day, dtRaw.Hour, 0, 0, DateTimeKind.Utc);

                    newCandles.Add(new AssetCandle
                    {
                        AssetId = asset.Id,
                        Exchange = exchange,
                        Interval = interval,
                        DateTime = dt,
                        Open = decimal.Parse(v.Open!, CultureInfo.InvariantCulture),
                        High = decimal.Parse(v.High!, CultureInfo.InvariantCulture),
                        Low = decimal.Parse(v.Low!, CultureInfo.InvariantCulture),
                        Close = decimal.Parse(v.Close!, CultureInfo.InvariantCulture)
                    });
                }
                catch (Exception ex)
                {
                    _logger.Debug("[UpdaterService] :: Parse error for {0} at {1}: {2}", symbol, v.Datetime ?? "null", ex.Message);
                }
            }

            if (newCandles.Count == 0)
            {
                _logger.Debug("[UpdaterService] :: No new candles for {0}", symbol);
                continue;
            }

            // Guardar candles usando BulkExtensions
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                var bulkConfig = new BulkConfig
                {
                    UpdateByProperties = new List<string> { "AssetId", "Exchange", "Interval", "DateTime" },
                    SetOutputIdentity = false,
                    UseTempDB = true
                };

                await _dbContext.BulkInsertOrUpdateAsync(newCandles, bulkConfig, cancellationToken: cancellationToken);

                // Crear candles USD si no es Forex
                if (!asset.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
                {
                    var usdCandles = newCandles.Select(c => new AssetCandleUSD
                    {
                        AssetId = c.AssetId,
                        Exchange = c.Exchange,
                        Interval = c.Interval,
                        DateTime = c.DateTime,
                        Open = c.Open * eurToUsd,
                        High = c.High * eurToUsd,
                        Low = c.Low * eurToUsd,
                        Close = c.Close * eurToUsd
                    }).ToList();

                    if (usdCandles.Count > 0)
                    {
                        var bulkUsdConfig = new BulkConfig
                        {
                            UpdateByProperties = new List<string> { "AssetId", "Exchange", "Interval", "DateTime" },
                            SetOutputIdentity = false,
                            UseTempDB = true
                        };

                        await _dbContext.BulkInsertOrUpdateAsync(usdCandles, bulkUsdConfig, cancellationToken: cancellationToken);
                    }
                }

                // Actualizar precio actual del activo
                var lastClose = newCandles.OrderByDescending(c => c.DateTime).First().Close;
                var lastCloseUsd = lastClose * eurToUsd;

                asset.UpdateCurrentPrice((double)lastClose, (double)lastCloseUsd);
                _unitOfWork.FinancialAssets.Update(asset);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                totalCandlesSaved += newCandles.Count;
                _logger.Debug("[UpdaterService] :: Saved {0} new candles for {1}", newCandles.Count, symbol);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.Debug("[UpdaterService] :: Error saving candles for {0}: {1}", symbol, ex.Message);
            }
        }

        _logger.Information("[UpdaterService] :: UpdateAssets completed: {0} candles saved for {1} assets", 
            totalCandlesSaved, selectedAssets.Count);
        _logger.Debug("[UpdaterService] :: UpdateAssetsAsync completed successfully ({0})", 
            marketHours ? "Mode market hours" : "Continuous mode");
    }

    public async Task CreateBetZonesAsync(bool marketHours, CancellationToken cancellationToken = default)
    {
        _logger.Debug("[UpdaterService] :: CreateBetZonesAsync called (MarketHours: {0})", marketHours);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Obtener activos según el modo
            var allAssets = await _unitOfWork.FinancialAssets.GetAllAsync(cancellationToken);
            var assetsQuery = !marketHours
                ? allAssets.Where(a => a.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase) ||
                                      a.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
                : allAssets;

            var financialAssets = assetsQuery.ToList();

            _logger.Debug("[UpdaterService] :: Found {0} financial assets to process", financialAssets.Count);

            if (financialAssets.Count == 0)
            {
                _logger.Debug("[UpdaterService] :: No financial assets found! Query returned empty list.");
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return;
            }

            int assetsProcessed = 0;
            foreach (var currentAsset in financialAssets)
            {
                assetsProcessed++;
                _logger.Debug("[UpdaterService] :: Processing asset {0}/{1}: {2} (ID: {3})",
                    assetsProcessed, financialAssets.Count, currentAsset.Ticker, currentAsset.Id);

                var now = DateTime.UtcNow;

                // Desactivar zonas existentes antes de crear nuevas
                var existingZones = await _unitOfWork.BetZones.DeactivateZonesByTickerAsync(currentAsset.Ticker, cancellationToken);
                var existingZonesUSD = await _unitOfWork.BetZonesUSD.DeactivateZonesByTickerAsync(currentAsset.Ticker, cancellationToken);

                if (existingZones > 0 || existingZonesUSD > 0)
                {
                    _logger.Debug("[UpdaterService] :: Deactivated {0} existing zones (EUR: {1}, USD: {2}) for {3} before creating new ones",
                        existingZones + existingZonesUSD, existingZones, existingZonesUSD, currentAsset.Ticker);
                }

                _logger.Debug("[UpdaterService] :: Starting zone creation process for {0}", currentAsset.Ticker);

                // Definir múltiples períodos temporales para cada timeframe
                var horizons = new Dictionary<int, List<(DateTime Start, DateTime End)>>();

                // Timeframe 1h: 3 períodos cortos
                horizons[1] = new List<(DateTime Start, DateTime End)>
                {
                    (now.AddHours(2), now.AddHours(5)),   // Muy corto: 3 horas
                    (now.AddHours(5), now.AddHours(10)),  // Corto: 5 horas
                    (now.AddHours(10), now.AddHours(18))  // Medio: 8 horas
                };

                // Timeframe 2h: 3 períodos
                horizons[2] = new List<(DateTime Start, DateTime End)>
                {
                    (now.AddHours(2), now.AddHours(8)),   // Corto: 6 horas
                    (now.AddHours(8), now.AddHours(16)),  // Medio: 8 horas
                    (now.AddHours(16), now.AddHours(28))  // Largo: 12 horas
                };

                // Timeframe 4h: 3 períodos
                horizons[4] = new List<(DateTime Start, DateTime End)>
                {
                    (now.AddHours(2), now.AddHours(10)),   // Corto: 8 horas
                    (now.AddHours(10), now.AddHours(22)),  // Medio: 12 horas
                    (now.AddHours(22), now.AddHours(40))   // Largo: 18 horas
                };

                // Timeframe 24h: 3 períodos
                horizons[24] = new List<(DateTime Start, DateTime End)>
                {
                    (now.AddHours(2), now.AddHours(26)),   // Corto: 24 horas (1 día)
                    (now.AddHours(26), now.AddHours(74)),  // Medio: 48 horas (2 días)
                    (now.AddHours(74), now.AddHours(146))  // Largo: 72 horas (3 días)
                };

                _logger.Debug("[UpdaterService] :: Starting to process {0} timeframes for {1}",
                    horizons.Count, currentAsset.Ticker);

                foreach (var timeframeEntry in horizons)
                {
                    int timeframe = timeframeEntry.Key;
                    var timePeriods = timeframeEntry.Value;

                    _logger.Debug("[UpdaterService] :: Processing timeframe {0} for {1}",
                        timeframe, currentAsset.Ticker);

                    // Obtener candles para análisis técnico
                    var candles = (await _unitOfWork.AssetCandles
                        .GetCandlesByAssetAsync(currentAsset.Id, "1h", 100, cancellationToken))
                        .OrderByDescending(c => c.DateTime)
                        .ToList();

                    _logger.Debug("[UpdaterService] :: Retrieved {0} candles for {1} timeframe {2}",
                        candles.Count, currentAsset.Ticker, timeframe);

                    if (candles.Count < 50)
                    {
                        _logger.Debug("[UpdaterService] :: Insufficient candles for {0} timeframe {1}. Found: {2}, Required: 50",
                            currentAsset.Ticker, timeframe, candles.Count);
                        continue;
                    }

                    // Preparar datos
                    var closes = candles.Select(c => (double)c.Close).Reverse().ToList();
                    var highs = candles.Select(c => (double)c.High).Reverse().ToList();
                    var lows = candles.Select(c => (double)c.Low).Reverse().ToList();
                    double currentPrice = closes.Last();

                    if (currentPrice <= 0 || double.IsNaN(currentPrice) || double.IsInfinity(currentPrice))
                    {
                        _logger.Debug("[UpdaterService] :: Invalid current price for {0} timeframe {1}: {2}",
                            currentAsset.Ticker, timeframe, currentPrice);
                        continue;
                    }

                    // Análisis exhaustivo de las últimas 10xT velas
                    int candlesToAnalyze = 30 * timeframe;
                    var timeframeCandles = candles.Take(Math.Min(candlesToAnalyze, candles.Count)).Reverse().ToList();
                    double maxVariationPercent = TechnicalAnalysisService.CalculateMaxVariationForTimeframe(timeframeCandles, currentPrice);

                    // Calcular retornos logarítmicos
                    var returns = TechnicalAnalysisService.CalculateLogReturns(closes);

                    // Calcular volatilidad EWMA
                    double volatility = TechnicalAnalysisService.CalculateEWMAVolatility(returns);
                    if (volatility == 0 || double.IsNaN(volatility) || double.IsInfinity(volatility))
                    {
                        double avgClose = closes.Average();
                        volatility = Math.Sqrt(closes.Average(c => Math.Pow(c - avgClose, 2))) / avgClose;
                        _logger.Debug("[UpdaterService] :: Using fallback volatility calculation for {0} timeframe {1}: {2}",
                            currentAsset.Ticker, timeframe, volatility);
                    }

                    if (volatility <= 0 || double.IsNaN(volatility) || double.IsInfinity(volatility))
                    {
                        _logger.Debug("[UpdaterService] :: Invalid volatility for {0} timeframe {1}: {2}. Skipping.",
                            currentAsset.Ticker, timeframe, volatility);
                        continue;
                    }

                    // Calcular drift (tendencia)
                    double drift = TechnicalAnalysisService.CalculateDrift(returns);

                    // Indicadores técnicos
                    double rsi = TechnicalAnalysisService.CalculateRSI(closes);
                    var bollinger = TechnicalAnalysisService.CalculateBollingerBands(closes);

                    // Detectar soportes y resistencias
                    List<double> supports;
                    List<double> resistances;
                    try
                    {
                        var result = TechnicalAnalysisService.DetectSupportResistance(highs, lows, closes);
                        supports = result.Supports;
                        resistances = result.Resistances;
                        _logger.Debug("[UpdaterService] :: Detected {0} supports and {1} resistances for {2}",
                            supports.Count, resistances.Count, currentAsset.Ticker);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug("[UpdaterService] :: Error detecting support/resistance for {0}: {1}", currentAsset.Ticker, ex.Message);
                        supports = new List<double>();
                        resistances = new List<double>();
                    }

                    // Obtener tipo de cambio EUR/USD
                    var eurUsdCandle = await _unitOfWork.AssetCandles
                        .GetLatestCandleAsync(223, "1h", cancellationToken); // EURUSD FOREX

                    var eurToUsd = eurUsdCandle != null ? (decimal)eurUsdCandle.Close : FIXED_EUR_USD;

                    // Generar zonas para cada período temporal
                    int totalZonesCreated = 0;
                    int maxPeriods = Math.Min(3, timePeriods.Count);

                    _logger.Debug("[UpdaterService] :: Generating zones for {0} timeframe {1} with {2} periods",
                        currentAsset.Ticker, timeframe, maxPeriods);

                    for (int periodIndex = 0; periodIndex < maxPeriods; periodIndex++)
                    {
                        var period = timePeriods[periodIndex];
                        double timeToExpiry = (period.End - now).TotalHours;

                        _logger.Debug("[UpdaterService] :: Calling GenerateIntelligentZones for {0} timeframe {1} period {2}. Price: {3}, Volatility: {4}, TimeToExpiry: {5}h",
                            currentAsset.Ticker, timeframe, periodIndex, currentPrice, volatility, timeToExpiry);

                        List<(double Target, double Margin, double BaseProbability, string ZoneType)> zones;
                        try
                        {
                            zones = GenerateIntelligentZones(
                                currentPrice, supports, resistances, volatility,
                                timeToExpiry, rsi, bollinger, drift, zoneCount: ZonesPerPeriod,
                                maxVariationPercent: maxVariationPercent);

                            _logger.Debug("[UpdaterService] :: GenerateIntelligentZones returned {0} zones for {1} timeframe {2} period {3}",
                                zones?.Count ?? 0, currentAsset.Ticker, timeframe, periodIndex);
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug("[UpdaterService] :: Error in GenerateIntelligentZones for {0} timeframe {1} period {2}: {3}",
                                currentAsset.Ticker, timeframe, periodIndex, ex.Message);
                            zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
                        }

                        if (zones == null || zones.Count == 0)
                        {
                            _logger.Debug("[UpdaterService] :: No zones generated for {0} timeframe {1} period {2}. Price: {3}, Volatility: {4}",
                                currentAsset.Ticker, timeframe, periodIndex, currentPrice, volatility);
                            continue;
                        }

                        // Ajustar probabilidades según distancia temporal
                        double timeAdjustmentFactor = 1.0 - (periodIndex * 0.08);
                        timeAdjustmentFactor = Math.Max(0.60, timeAdjustmentFactor);

                        // Convertir márgenes absolutos a porcentuales
                        var zonesWithPercentMargins = zones.Select(z => (
                            z.Target,
                            MarginPercent: (z.Margin / z.Target) * 100.0,
                            z.BaseProbability,
                            z.ZoneType
                        )).ToList();

                        zonesWithPercentMargins = AdjustZonesToTouchPercent(zonesWithPercentMargins);

                        // Crear zonas EUR para este período
                        int betTypeCounter = 0;
                        foreach (var zone in zonesWithPercentMargins)
                        {
                            if (zone.Target <= 0 || zone.MarginPercent <= 0)
                            {
                                _logger.Debug("[UpdaterService] :: Invalid zone data for {0}: Target={1}, MarginPercent={2}",
                                    currentAsset.Ticker, zone.Target, zone.MarginPercent);
                                continue;
                            }

                            double adjustedProb = zone.BaseProbability * timeAdjustmentFactor;
                            double baseOdds = TechnicalAnalysisService.ProbabilityToOdds(adjustedProb, 0.95);
                            double odds = ApplyTimeframeOddsBoost(baseOdds, timeframe);

                            var betZone = new BetZone(
                                currentAsset.Ticker,
                                zone.Target,
                                Math.Round(zone.MarginPercent, 1),
                                period.Start,
                                period.End,
                                Math.Round(odds, 2),
                                betTypeCounter % 2,
                                timeframe
                            );

                            await _unitOfWork.BetZones.AddAsync(betZone, cancellationToken);
                            betTypeCounter++;
                        }

                        // Crear zonas USD para este período
                        betTypeCounter = 0;
                        foreach (var zone in zonesWithPercentMargins)
                        {
                            if (zone.Target <= 0 || zone.MarginPercent <= 0)
                                continue;

                            double adjustedProb = zone.BaseProbability * timeAdjustmentFactor;
                            double baseOddsUsd = TechnicalAnalysisService.ProbabilityToOdds(adjustedProb, 0.95);
                            double odds = ApplyTimeframeOddsBoost(baseOddsUsd, timeframe);

                            var betZoneUSD = new BetZoneUSD(
                                currentAsset.Ticker,
                                zone.Target * (double)eurToUsd,
                                Math.Round(zone.MarginPercent, 1),
                                period.Start,
                                period.End,
                                Math.Round(odds, 2),
                                betTypeCounter % 2,
                                timeframe
                            );

                            await _unitOfWork.BetZonesUSD.AddAsync(betZoneUSD, cancellationToken);
                            betTypeCounter++;
                        }

                        totalZonesCreated += zonesWithPercentMargins.Count;

                        _logger.Debug("[UpdaterService] :: Created {0} zones for period {1} ({2} to {3}) for {4} timeframe {5}",
                            zonesWithPercentMargins.Count, periodIndex, period.Start, period.End, currentAsset.Ticker, timeframe);
                    }

                    _logger.Debug("[UpdaterService] :: Created TOTAL {0} zones across {1} time periods for {2} timeframe {3}",
                        totalZonesCreated, timePeriods.Count, currentAsset.Ticker, timeframe);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Actualizar current_max_odd después de crear zonas
            await UpdateCurrentMaxOddsAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: CreateBetZonesAsync completed successfully with technical analysis. ({0})",
                marketHours ? "Mode market hours" : "Continuous mode");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: CreateBetZonesAsync error. Message: {0}, StackTrace: {1}",
                ex.Message ?? "Unknown", ex.StackTrace ?? "No stack trace");
            throw;
        }
    }

    public async Task CheckBetsAsync(bool marketHours, CancellationToken cancellationToken = default)
    {
        _logger.Debug("[UpdaterService] :: CheckBetsAsync called (MarketHours: {0})", marketHours);

        try
        {
            var now = DateTime.UtcNow;

            // Obtener IDs de zonas de apuestas activas
            var betZoneIds = await _unitOfWork.BetZones
                .GetActiveBetZoneIdsByDateRangeAsync(now, now, marketHours, cancellationToken);

            if (!betZoneIds.Any())
            {
                _logger.Debug("[UpdaterService] :: CheckBetsAsync - No bet zones to check");
                return;
            }

            // Obtener apuestas no finalizadas para estas zonas
            var betsToUpdate = await _unitOfWork.Bets
                .GetBetsByBetZoneIdsAsync(betZoneIds, includeFinished: false, cancellationToken);

            foreach (var bet in betsToUpdate)
            {
                var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
                if (betZone == null)
                {
                    _logger.Debug("[UpdaterService] :: CheckBetsAsync - Bet zone null for bet [{0}]", bet.Id);
                    continue;
                }

                var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(bet.Ticker, cancellationToken);
                if (asset == null)
                {
                    _logger.Debug("[UpdaterService] :: CheckBetsAsync - Asset null for bet [{0}]", bet.Ticker);
                    continue;
                }

                // Obtener candles en el rango de la zona
                var candles = await _unitOfWork.AssetCandles
                    .GetCandlesByDateRangeAsync(asset.Id, "1h", betZone.StartDate, betZone.EndDate, cancellationToken);

                if (!candles.Any())
                {
                    _logger.Debug("[UpdaterService] :: CheckBetsAsync - No candles for [{0}] zone [{1}]", 
                        asset.Ticker, betZone.Id);
                    continue;
                }

                // Calcular l?mites de la zona
                double upperBound = betZone.TargetValue + (betZone.TargetValue * betZone.BetMargin / 200);
                double lowerBound = betZone.TargetValue - (betZone.TargetValue * betZone.BetMargin / 200);

                // Verificar si alguna vela sali? de la zona
                bool hasExitedZone = candles.Any(c =>
                    (double)c.High > upperBound || (double)c.Low < lowerBound);

                // Usar m?todos de dominio
                if (hasExitedZone)
                {
                    bet.MarkAsLost();
                }
                else
                {
                    // Si no sali? de la zona, la apuesta sigue activa (no se marca como ganada hasta que termine el per?odo)
                    // En el c?digo legacy se marca target_won = true, pero no se marca finished hasta que salga de la zona
                    // Por ahora, solo marcamos como perdida si sale de la zona
                }

                _unitOfWork.Bets.Update(bet);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: CheckBetsAsync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Debug("[UpdaterService] :: CheckBetsAsync error: {0}", ex.Message);
            throw;
        }
    }

    public async Task RefreshTargetOddsAsync(CancellationToken cancellationToken = default)
    {
        _logger.Debug("[UpdaterService] :: RefreshTargetOddsAsync called");

        try
        {
            var now = DateTime.UtcNow;

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Obtener zonas activas que aún no han comenzado
            var zones = await _unitOfWork.BetZones.GetActiveFutureBetZonesAsync(now, cancellationToken);

            // Agrupar por ticker, timeframe y start_date
            var groups = zones
                .GroupBy(z => new { z.Ticker, z.Timeframe, z.StartDate })
                .ToList();

            foreach (var group in groups)
            {
                var zoneIds = group.Select(z => z.Id).ToList();

                // Obtener volumen de apuestas por zona
                var volumes = await _unitOfWork.Bets.GetBetVolumesByZoneIdsAsync(zoneIds, cancellationToken);

                if (volumes.Count == 0)
                    continue;

                // Calcular odds basadas en volumen
                double k = 1.0;
                double margin = 0.98;
                double total = volumes.Values.Sum(v => v + k);

                foreach (var volumeEntry in volumes)
                {
                    double prob = (volumeEntry.Value + k) / total;
                    double baseOdds = Math.Max(1.1, Math.Round((1.0 / prob) * margin, 2));
                    double odds = ApplyTimeframeOddsBoost(baseOdds, group.Key.Timeframe);

                    var zone = group.FirstOrDefault(z => z.Id == volumeEntry.Key);
                    if (zone != null)
                    {
                        zone.UpdateTargetOdds(odds);
                        _unitOfWork.BetZones.Update(zone);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Actualizar current_max_odd después de refrescar las odds
            await UpdateCurrentMaxOddsAsync(cancellationToken);

            _logger.Debug("[UpdaterService] :: RefreshTargetOddsAsync completed successfully");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: RefreshTargetOddsAsync error: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateOddsForBetZoneAsync(int betZoneId, string currency, CancellationToken cancellationToken = default)
    {
        const double k = 1.0;
        const double margin = 0.98;

        if (currency == "USD")
        {
            var zone = await _unitOfWork.BetZonesUSD.GetByIdAsync(betZoneId, cancellationToken);
            if (zone == null) return;

            var zonesSameTickerTf = await _unitOfWork.BetZonesUSD.GetActiveBetZonesByTickerAsync(zone.Ticker, zone.Timeframe, cancellationToken);
            var group = zonesSameTickerTf.Where(z => z.StartDate == zone.StartDate).ToList();
            if (group.Count == 0) return;

            var zoneIds = group.Select(z => z.Id).ToList();
            var volumes = await _unitOfWork.Bets.GetBetVolumesByZoneIdsAsync(zoneIds, cancellationToken);
            if (volumes.Count == 0) return;

            double total = volumes.Values.Sum(v => v + k);
            foreach (var volEntry in volumes)
            {
                double prob = (volEntry.Value + k) / total;
                double baseOddsVol = Math.Max(1.1, Math.Round((1.0 / prob) * margin, 2));
                double odds = ApplyTimeframeOddsBoost(baseOddsVol, zone.Timeframe);
                var z = group.FirstOrDefault(x => x.Id == volEntry.Key);
                if (z != null)
                {
                    z.UpdateTargetOdds(odds);
                    _unitOfWork.BetZonesUSD.Update(z);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: UpdateOddsForBetZoneAsync (USD) completed for zone {0}, ticker {1}", betZoneId, zone.Ticker);
        }
        else
        {
            var zone = await _unitOfWork.BetZones.GetByIdAsync(betZoneId, cancellationToken);
            if (zone == null) return;

            var zonesSameTickerTf = await _unitOfWork.BetZones.GetActiveBetZonesByTickerAsync(zone.Ticker, zone.Timeframe, cancellationToken);
            var group = zonesSameTickerTf.Where(z => z.StartDate == zone.StartDate).ToList();
            if (group.Count == 0) return;

            var zoneIds = group.Select(z => z.Id).ToList();
            var volumes = await _unitOfWork.Bets.GetBetVolumesByZoneIdsAsync(zoneIds, cancellationToken);
            if (volumes.Count == 0) return;

            double total = volumes.Values.Sum(v => v + k);
            foreach (var volEntry in volumes)
            {
                double prob = (volEntry.Value + k) / total;
                double baseOddsVolEur = Math.Max(1.1, Math.Round((1.0 / prob) * margin, 2));
                double odds = ApplyTimeframeOddsBoost(baseOddsVolEur, zone.Timeframe);
                var z = group.FirstOrDefault(x => x.Id == volEntry.Key);
                if (z != null)
                {
                    z.UpdateTargetOdds(odds);
                    _unitOfWork.BetZones.Update(z);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.Debug("[UpdaterService] :: UpdateOddsForBetZoneAsync (EUR) completed for zone {0}, ticker {1}", betZoneId, zone.Ticker);
        }

        await UpdateCurrentMaxOddsAsync(cancellationToken);
    }

    /// <summary>Rellena el servicio en memoria desde las zonas activas en BD. Llamar al arranque para que la API Trends devuelva datos de inmediato.</summary>
    public Task RefreshMaxOddsFromDatabaseAsync(CancellationToken cancellationToken = default) =>
        UpdateCurrentMaxOddsAsync(cancellationToken);

    private async Task UpdateCurrentMaxOddsAsync(CancellationToken cancellationToken = default)
    {
        _logger.Debug("[UpdaterService] :: UpdateCurrentMaxOddsAsync called");

        try
        {
            var allAssets = await _unitOfWork.FinancialAssets.GetAllAsync(cancellationToken);
            var eurData = new Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>>(StringComparer.OrdinalIgnoreCase);
            var usdData = new Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in allAssets)
            {
                var ticker = asset.Ticker ?? string.Empty;
                if (string.IsNullOrEmpty(ticker)) continue;

                var activeBetZonesEUR = await _unitOfWork.BetZones.GetActiveBetZonesByTickerAsync(ticker, 0, cancellationToken);
                var activeBetZonesUSD = await _unitOfWork.BetZonesUSD.GetActiveBetZonesByTickerAsync(ticker, 0, cancellationToken);

                foreach (var tf in Timeframes)
                {
                    var zonesEur = activeBetZonesEUR.Where(z => z.Timeframe == tf).ToList();
                    var zonesUsd = activeBetZonesUSD.Where(z => z.Timeframe == tf).ToList();

                    if (zonesEur.Any())
                    {
                        var maxZoneEur = zonesEur.OrderByDescending(z => z.TargetOdds).First();
                        var direction = ComputeDirection(asset.CurrentEur, maxZoneEur.TargetValue, maxZoneEur.BetMargin);
                        if (!eurData.ContainsKey(ticker))
                            eurData[ticker] = new Dictionary<int, (double, int, int)>();
                        eurData[ticker][tf] = (maxZoneEur.TargetOdds, direction, maxZoneEur.Id);
                    }

                    if (zonesUsd.Any())
                    {
                        var maxZoneUsd = zonesUsd.OrderByDescending(z => z.TargetOdds).First();
                        var direction = ComputeDirection(asset.CurrentUsd, maxZoneUsd.TargetValue, maxZoneUsd.BetMargin);
                        if (!usdData.ContainsKey(ticker))
                            usdData[ticker] = new Dictionary<int, (double, int, int)>();
                        usdData[ticker][tf] = (maxZoneUsd.TargetOdds, direction, maxZoneUsd.Id);
                    }
                }
            }

            var eurReadOnly = eurData.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>)new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(kv.Value));
            var usdReadOnly = usdData.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>)new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(kv.Value));
            _tickerMaxOddsService.ReplaceAllEur(eurReadOnly);
            _tickerMaxOddsService.ReplaceAllUsd(usdReadOnly);
            _logger.Debug("[UpdaterService] :: UpdateCurrentMaxOddsAsync completed successfully (EUR: {EurTickers} tickers, USD: {UsdTickers} tickers)",
                eurData.Count, usdData.Count);
        }
        catch (Exception ex)
        {
            _logger.Debug("[UpdaterService] :: UpdateCurrentMaxOddsAsync error: {0}", ex.Message);
            throw;
        }
    }

    private static int ComputeDirection(double currentPrice, double targetValue, double betMargin)
    {
        double halfMargin = (betMargin / 200.0) * targetValue;
        double upperBound = targetValue + halfMargin;
        double lowerBound = targetValue - halfMargin;
        if (lowerBound > currentPrice) return 1;   // Verde: zona por encima
        if (upperBound < currentPrice) return -1;   // Rojo: zona por debajo
        return 0;   // Amarillo: zona en medio
    }

    /// <summary>Obsoleto: ya no se usa tabla Trends; la API calcula los 5 con mayor odd en tiempo real.</summary>
    [Obsolete("Trends are computed on-demand in the API.")]
    public Task UpdateTrendsAsync(bool marketHours, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    // ========== MÉTODOS AUXILIARES DE ANÁLISIS TÉCNICO ==========

    /// <summary>
    /// 9 zonas con 9 targets distintos, intervalos [target ± margin] sin solapamiento (hueco mínimo entre bandas).
    /// </summary>
    private static List<(double Target, double Margin, double BaseProbability, string ZoneType)>
        BuildGuaranteedNineNonOverlappingZones(
            double currentPrice,
            double effectiveVolatility,
            double timeToExpiryHours,
            double drift,
            double rsi,
            double maxVariationPercent)
    {
        // Tres bloques: 3 debajo del spot, 3 en el centro (spot + vecinos), 3 encima — todos los targets son distintos
        (double pct, string zoneType)[] spec =
        {
            (-0.052, "below"),
            (-0.035, "below"),
            (-0.018, "below"),
            (-0.006, "below"),
            (0.0, "current"),
            (0.006, "above"),
            (0.018, "above"),
            (0.035, "above"),
            (0.052, "above"),
        };

        var centers = new double[spec.Length];
        for (int i = 0; i < spec.Length; i++)
            centers[i] = currentPrice * (1.0 + spec[i].pct);

        double minGap = double.MaxValue;
        for (int i = 1; i < centers.Length; i++)
            minGap = Math.Min(minGap, centers[i] - centers[i - 1]);

        double volBoost = 1.0 + Math.Min(0.4, maxVariationPercent);
        double minHalf = currentPrice * 0.0025;
        double maxHalf = currentPrice * Math.Min(0.04, Math.Max(0.01, effectiveVolatility * 0.14 * volBoost));
        // Sin solape: para todo i, margin_i + margin_{i+1} <= (c_{i+1} - c_i); con m uniforme: 2m <= minGap
        double uniformMargin = Math.Clamp(minGap * 0.45, minHalf, maxHalf);
        uniformMargin = Math.Min(uniformMargin, minGap * 0.49);

        var zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
        for (int i = 0; i < centers.Length; i++)
        {
            double targetPrice = centers[i];
            double margin = uniformMargin;

            double prob = 0.5;
            try
            {
                prob = TechnicalAnalysisService.CalculateReachProbability(
                    currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
            }
            catch
            {
                prob = 0.5;
            }

            double targetPct = (targetPrice - currentPrice) / currentPrice;
            if (targetPct < 0 && rsi < 30) prob *= 1.12;
            if (targetPct > 0 && rsi > 70) prob *= 0.88;
            // Evitar que todas las colas “muertas” converjan al mismo valor (antes 0.14) y generen odds idénticas.
            prob = Math.Max(0.025, Math.Min(0.90, prob));

            zones.Add((targetPrice, margin, prob, spec[i].zoneType));
        }

        // Unificación de márgenes por parejas adyacentes: mismo half-width base salvo ajuste fino
        return EnforcePairwiseNonOverlappingMargins(zones, currentPrice, minHalf);
    }

    /// <summary>
    /// Ajusta márgenes para que para todas las parejas ordenadas por Target se cumpla upper_i &lt; lower_{i+1} (sin solape).
    /// </summary>
    private static List<(double Target, double Margin, double BaseProbability, string ZoneType)>
        EnforcePairwiseNonOverlappingMargins(
            List<(double Target, double Margin, double BaseProbability, string ZoneType)> zones,
            double currentPrice,
            double minHalf)
    {
        if (zones.Count <= 1)
            return zones;

        var ordered = zones.OrderBy(z => z.Target).ToList();
        var margins = ordered.Select(z => z.Margin).ToArray();
        int n = ordered.Count;

        for (int pass = 0; pass < 24; pass++)
        {
            bool changed = false;
            for (int i = 0; i < n - 1; i++)
            {
                double upperI = ordered[i].Target + margins[i];
                double lowerNext = ordered[i + 1].Target - margins[i + 1];
                double epsilon = Math.Max(currentPrice * 1e-9, minHalf * 1e-4);
                if (upperI >= lowerNext - epsilon)
                {
                    double overlap = upperI - lowerNext + epsilon;
                    double shrink = overlap * 0.51;
                    double ni = Math.Max(minHalf, margins[i] - shrink);
                    double nj = Math.Max(minHalf, margins[i + 1] - shrink);
                    if (ni < margins[i] - 1e-15 || nj < margins[i + 1] - 1e-15)
                        changed = true;
                    margins[i] = ni;
                    margins[i + 1] = nj;
                }
            }
            if (!changed) break;
        }

        var result = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
        for (int i = 0; i < n; i++)
        {
            var z = ordered[i];
            result.Add((z.Target, margins[i], z.BaseProbability, z.ZoneType));
        }

        return result;
    }

    /// <summary>
    /// Genera zonas inteligentes basadas en niveles técnicos
    /// Genera zoneCount zonas VISUALMENTE DISTINTAS distribuidas en diferentes niveles de precio
    /// </summary>
    private static List<(double Target, double Margin, double BaseProbability, string ZoneType)> GenerateIntelligentZones(
        double currentPrice, List<double> supports, List<double> resistances,
        double volatility, double timeToExpiryHours, double rsi,
        (double Upper, double Middle, double Lower) bollinger, double drift, int zoneCount = 6,
        double maxVariationPercent = 0.0)
    {
        var zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();

        // Validación temprana
        if (currentPrice <= 0 || double.IsNaN(currentPrice) || double.IsInfinity(currentPrice))
        {
            return zones;
        }

        // No forzar σ ≥ 1%: en datos horarios muchos activos quedaban con la misma volatilidad efectiva
        // y las probabilidades de alcance (y las odds máximas) coincidían entre tickers.
        double effectiveVolatility = (volatility > 1e-9 && !double.IsNaN(volatility) && !double.IsInfinity(volatility))
            ? volatility
            : 0.001;

        if (zoneCount >= 9)
        {
            return BuildGuaranteedNineNonOverlappingZones(
                currentPrice, effectiveVolatility, timeToExpiryHours, drift, rsi, maxVariationPercent);
        }

        // Definir porcentajes de distancia del precio actual
        var zonePercentages = new List<double>();

        if (zoneCount <= 3)
        {
            zonePercentages = zoneCount switch
            {
                2 => new List<double> { -0.025, 0.025 },
                3 => new List<double> { -0.04, 0.04 },
                _ => new List<double> { -0.04, 0.04 }
            };
        }
        else
        {
            zonePercentages.Add(0.0);

            for (double pct = -0.06; pct <= -0.01; pct += 0.0075)
            {
                zonePercentages.Add(pct);
            }

            for (double pct = 0.01; pct <= 0.06; pct += 0.0075)
            {
                zonePercentages.Add(pct);
            }

            if (timeToExpiryHours > 12)
            {
                zonePercentages.AddRange(new[] { -0.09, -0.075, -0.10, 0.075, 0.09, 0.10 });
            }
        }

        // ZONA ACTUAL (0% - precio actual)
        double currentZoneMargin = zoneCount == 3
            ? Math.Max(currentPrice * 0.0075, currentPrice * effectiveVolatility * 0.01)
            : Math.Max(currentPrice * 0.01, currentPrice * effectiveVolatility * 0.0125);
        double currentProb = 0.88;
        zones.Add((currentPrice, currentZoneMargin, currentProb, "current"));

        // Generar zonas distribuidas VISUALMENTE
        var usedPercentages = new HashSet<double> { 0.0 };

        // Buscar soportes/resistencias y mapearlos
        var technicalLevels = new List<(double Price, double Percentage, string Type)>();

        foreach (var support in supports.Where(s => s > 0 && s < currentPrice * 1.5))
        {
            double pct = (support - currentPrice) / currentPrice;
            if (pct >= -0.10 && pct <= 0.10)
                technicalLevels.Add((support, pct, "support"));
        }

        foreach (var resistance in resistances.Where(r => r > 0 && r > currentPrice * 0.5))
        {
            double pct = (resistance - currentPrice) / currentPrice;
            if (pct >= -0.10 && pct <= 0.10)
                technicalLevels.Add((resistance, pct, "resistance"));
        }

        if (bollinger.Lower > 0)
        {
            double pct = (bollinger.Lower - currentPrice) / currentPrice;
            if (pct >= -0.10 && pct <= 0.10)
                technicalLevels.Add((bollinger.Lower, pct, "bollinger_lower"));
        }
        if (bollinger.Upper > 0)
        {
            double pct = (bollinger.Upper - currentPrice) / currentPrice;
            if (pct >= -0.10 && pct <= 0.10)
                technicalLevels.Add((bollinger.Upper, pct, "bollinger_upper"));
        }

        // Para cada porcentaje objetivo, buscar nivel técnico más cercano o usar porcentaje directamente
        var orderedTargetPcts = zonePercentages.Where(p => p != 0.0).OrderBy(p => Math.Abs(p)).ToList();

        int maxZoneAttempts = zoneCount * 10;
        int zoneAttempts = 0;
        foreach (var targetPct in orderedTargetPcts)
        {
            if (zones.Count >= zoneCount) break;
            if (zoneAttempts++ >= maxZoneAttempts) break;

            bool nearSpotBand = zoneCount >= 9 && Math.Abs(targetPct) < 0.012;

            double targetPrice;
            string zoneType;
            double margin;

            var closestTechnical = technicalLevels
                .Where(t => Math.Abs(t.Percentage - targetPct) < 0.0075)
                .OrderBy(t => Math.Abs(t.Percentage - targetPct))
                .FirstOrDefault();

            if (closestTechnical.Price > 0)
            {
                targetPrice = closestTechnical.Price;
                zoneType = closestTechnical.Type;
                double baseMargin = targetPrice * 0.01;
                double volatilityMargin = targetPrice * effectiveVolatility * 0.015;
                margin = Math.Max(baseMargin, volatilityMargin);
            }
            else
            {
                targetPrice = currentPrice * (1.0 + targetPct);
                zoneType = targetPct < 0 ? "below" : "above";

                double distanceFromCurrent = Math.Abs(targetPct);
                double baseMinMarginPercent = zoneCount == 3
                    ? 0.0075
                    : nearSpotBand ? 0.0055 : 0.01;
                double baseMaxMarginPercent = zoneCount == 3
                    ? 0.025
                    : nearSpotBand ? 0.018 : 0.04;

                double minMarginPercent = baseMinMarginPercent;
                double maxMarginPercent = baseMaxMarginPercent;

                if (maxVariationPercent > 0)
                {
                    double variationBasedMax = Math.Max(maxVariationPercent * 0.5, baseMaxMarginPercent);
                    variationBasedMax = Math.Min(variationBasedMax, maxVariationPercent * 2.0);
                    variationBasedMax = Math.Max(variationBasedMax, baseMaxMarginPercent);
                    maxMarginPercent = variationBasedMax;
                    minMarginPercent = Math.Max(baseMinMarginPercent, maxMarginPercent * 0.3);
                }

                double marginPercent = minMarginPercent + (distanceFromCurrent / 0.10) * (maxMarginPercent - minMarginPercent);
                marginPercent = Math.Min(maxMarginPercent, Math.Max(minMarginPercent, marginPercent));
                margin = targetPrice * marginPercent;
                double absoluteMinMargin = targetPrice * (zoneCount == 3
                    ? 0.006
                    : nearSpotBand ? 0.005 : 0.0075);
                margin = Math.Max(margin, absoluteMinMargin);
            }

            // Verificar solapamiento
            bool overlaps = zones.Any(z =>
            {
                double zUpper = z.Target + z.Margin;
                double zLower = z.Target - z.Margin;
                double targetUpper = targetPrice + margin;
                double targetLower = targetPrice - margin;
                double overlapAmount = Math.Min(targetUpper - zLower, zUpper - targetLower);
                double overlapFrac = zoneCount == 3
                    ? 0.2
                    : nearSpotBand ? 0.38 : 0.1;
                return overlapAmount > (Math.Min(margin, z.Margin) * overlapFrac);
            });

            if (overlaps)
                continue;

            // Calcular probabilidad
            double prob = 0.5;
            try
            {
                prob = TechnicalAnalysisService.CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
            }
            catch
            {
                prob = 0.5;
            }

            // Ajustar probabilidad según indicadores técnicos
            if (targetPct < 0 && rsi < 30) prob *= 1.15;
            if (targetPct > 0 && rsi > 70) prob *= 0.85;

            double distanceFactor = 1.0 - (Math.Abs(targetPct) * 0.3);
            prob *= Math.Max(0.5, distanceFactor);
            prob = Math.Max(0.03, Math.Min(0.88, prob));

            if (prob < 0.30)
            {
                margin *= 1.3;
            }

            double finalMinMargin = targetPrice * (nearSpotBand ? 0.006 : 0.01);
            margin = Math.Max(margin, finalMinMargin);

            zones.Add((targetPrice, margin, prob, zoneType));
            usedPercentages.Add(targetPct);
        }

        // Si aún no tenemos suficientes zonas, completar con zonas distribuidas uniformemente
        int maxIterations = 100;
        int iterations = 0;
        while (zones.Count < zoneCount && iterations < maxIterations)
        {
            iterations++;
            int missingZones = zoneCount - zones.Count;
            double step = 0.075 / (missingZones + 1);

            int zonesAddedThisIteration = 0;
            for (int i = 1; i <= missingZones && zones.Count < zoneCount; i++)
            {
                bool isUp = i % 2 == 0;
                double pct = isUp ? step * i : -step * i;

                if (usedPercentages.Any(up => Math.Abs(up - pct) < 0.01))
                    continue;

                double targetPrice = currentPrice * (1.0 + pct);
                double distanceFromCurrent = Math.Abs(pct);
                double baseMinMarginPercent = 0.01;
                double baseMaxMarginPercent = 0.04;

                double minMarginPercent = baseMinMarginPercent;
                double maxMarginPercent = baseMaxMarginPercent;
                if (maxVariationPercent > 0)
                {
                    double variationBasedMax = Math.Max(maxVariationPercent * 0.5, baseMaxMarginPercent);
                    variationBasedMax = Math.Min(variationBasedMax, maxVariationPercent * 2.0);
                    maxMarginPercent = Math.Max(variationBasedMax, baseMaxMarginPercent);
                    minMarginPercent = Math.Max(baseMinMarginPercent, maxMarginPercent * 0.3);
                }

                double marginPercent = minMarginPercent + (distanceFromCurrent / 0.10) * (maxMarginPercent - minMarginPercent);
                marginPercent = Math.Min(maxMarginPercent, Math.Max(minMarginPercent, marginPercent));
                double tempMargin = targetPrice * marginPercent;
                tempMargin = Math.Max(tempMargin, targetPrice * 0.01);

                bool overlaps = zones.Any(z =>
                {
                    double zUpper = z.Target + z.Margin;
                    double zLower = z.Target - z.Margin;
                    double targetUpper = targetPrice + tempMargin;
                    double targetLower = targetPrice - tempMargin;
                    double gap = Math.Min(targetLower - zUpper, zLower - targetUpper);
                    return gap < 0 && Math.Abs(gap) > (tempMargin * 0.1);
                });

                if (overlaps)
                    continue;

                double prob = 0.5;
                try
                {
                    prob = TechnicalAnalysisService.CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
                    prob = Math.Max(0.20, Math.Min(0.60, prob * (1.0 - Math.Abs(pct) * 0.4)));
                }
                catch
                {
                    prob = 0.5;
                }

                zones.Add((targetPrice, tempMargin, prob, isUp ? "above" : "below"));
                usedPercentages.Add(pct);
                zonesAddedThisIteration++;
            }

            if (zonesAddedThisIteration == 0)
                break;

            if (zones.Count == 0)
            {
                double minMargin = currentPrice * 0.01;
                zones.Add((currentPrice, minMargin, 0.5, "current"));
            }
        }

        // Ordenar por precio
        var sortedZones = zones.OrderBy(z => z.Target).ToList();
        var zonesBelow = sortedZones.Where(z => z.Target < currentPrice).ToList();
        var zonesAbove = sortedZones.Where(z => z.Target > currentPrice).ToList();
        var currentZone = sortedZones.FirstOrDefault(z => Math.Abs(z.Target - currentPrice) / currentPrice < 0.001);
        bool hasCurrentZone = currentZone.Target > 0 && !double.IsNaN(currentZone.Target);

        var finalZones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();

        if (sortedZones.Count <= zoneCount)
        {
            return sortedZones.Take(zoneCount).ToList();
        }

        // Seleccionar zonas según el número solicitado
        if (zoneCount == 2)
        {
            if (zonesBelow.Any())
                finalZones.Add(zonesBelow.OrderByDescending(z => z.Target).First());
            if (zonesAbove.Any())
                finalZones.Add(zonesAbove.OrderBy(z => z.Target).First());
            if (finalZones.Count < 2 && hasCurrentZone)
                finalZones.Add(currentZone);
        }
        else if (zoneCount == 3)
        {
            if (zonesBelow.Any())
                finalZones.Add(zonesBelow.OrderByDescending(z => z.Target).First());
            if (hasCurrentZone)
                finalZones.Add(currentZone);
            else if (zonesBelow.Any() || zonesAbove.Any())
            {
                var closest = sortedZones.OrderBy(z => Math.Abs(z.Target - currentPrice)).First();
                finalZones.Add(closest);
            }
            if (zonesAbove.Any())
                finalZones.Add(zonesAbove.OrderBy(z => z.Target).First());
        }
        else
        {
            if (hasCurrentZone && zoneCount > 3)
                finalZones.Add(currentZone);

            int zonesNeeded = zoneCount - finalZones.Count;
            int zonesBelowCount = zonesNeeded / 2;
            int zonesAboveCount = zonesNeeded - zonesBelowCount;

            finalZones.AddRange(zonesBelow.OrderByDescending(z => z.Target).Take(zonesBelowCount));
            finalZones.AddRange(zonesAbove.OrderBy(z => z.Target).Take(zonesAboveCount));

            var remaining = sortedZones
                .Where(z => !finalZones.Any(fz => Math.Abs(fz.Target - z.Target) / currentPrice < 0.001))
                .OrderByDescending(z => z.BaseProbability)
                .Take(zoneCount - finalZones.Count);

            finalZones.AddRange(remaining);
        }

        if (finalZones.Count == 0 && sortedZones.Count > 0)
        {
            finalZones.Add(sortedZones.First());
        }

        return finalZones.OrderBy(z => z.Target).Take(Math.Max(1, zoneCount)).ToList();
    }

    /// <summary>
    /// Ajusta los márgenes porcentuales y precios objetivo de las zonas para que se toquen en sus extremos sin solaparse
    /// </summary>
    private static List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)> AdjustZonesToTouchPercent(
        List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)> zones)
    {
        if (zones.Count <= 1)
            return zones;

        // Identificar la zona actual (amarilla)
        var currentZone = zones.FirstOrDefault(z => z.ZoneType == "current");
        if (currentZone.Target == 0)
        {
            double avgTarget = zones.Average(z => z.Target);
            currentZone = zones.OrderBy(z => Math.Abs(z.Target - avgTarget)).First();
        }

        // Calcular límites de la zona amarilla
        double yellowUpperBound = currentZone.Target * (1.0 + currentZone.MarginPercent / 200.0);
        double yellowLowerBound = currentZone.Target * (1.0 - currentZone.MarginPercent / 200.0);

        // Separar zonas en rojas (inferiores), amarilla (actual) y verdes (superiores)
        var redZones = zones.Where(z => (z.Target < currentZone.Target || z.ZoneType == "below") &&
                                        !(z.ZoneType == "current" || Math.Abs(z.Target - currentZone.Target) < 0.0001))
                            .OrderByDescending(z => z.Target).ToList();
        var greenZones = zones.Where(z => (z.Target > currentZone.Target || z.ZoneType == "above") &&
                                          !(z.ZoneType == "current" || Math.Abs(z.Target - currentZone.Target) < 0.0001))
                              .OrderBy(z => z.Target).ToList();

        var adjustedZones = new List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)>();

        // Ajustar zonas rojas (inferiores)
        const double gapPercent = 0.15;
        double currentLowerBound = yellowLowerBound;
        foreach (var zone in redZones)
        {
            double gapAdjustedBound = currentLowerBound * (1.0 - gapPercent / 100.0);
            double denominator = 1.0 + (zone.MarginPercent / 200.0);
            double adjustedTarget = gapAdjustedBound / denominator;
            double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent);

            adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
            currentLowerBound = adjustedTarget * (1.0 - adjustedMarginPercent / 200.0) * (1.0 - gapPercent / 100.0);
        }

        // Añadir zona amarilla (actual)
        adjustedZones.Add((currentZone.Target, Math.Max(1.0, currentZone.MarginPercent), currentZone.BaseProbability, currentZone.ZoneType));

        // Ajustar zonas verdes (superiores)
        double currentUpperBound = yellowUpperBound;
        foreach (var zone in greenZones)
        {
            double gapAdjustedBound = currentUpperBound * (1.0 + gapPercent / 100.0);
            double denominator = 1.0 - (zone.MarginPercent / 200.0);
            if (denominator > 0.001)
            {
                double adjustedTarget = gapAdjustedBound / denominator;
                double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent);

                adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
                currentUpperBound = adjustedTarget * (1.0 + adjustedMarginPercent / 200.0) * (1.0 + gapPercent / 100.0);
            }
            else
            {
                double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent * 0.5);
                denominator = 1.0 - (adjustedMarginPercent / 200.0);
                double adjustedTarget = gapAdjustedBound / denominator;

                adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
                currentUpperBound = adjustedTarget * (1.0 + adjustedMarginPercent / 200.0) * (1.0 + gapPercent / 100.0);
            }
        }

        // Ordenar zonas ajustadas por precio
        adjustedZones = adjustedZones.OrderBy(z => z.Target).ToList();

        // Verificar y ajustar que todas las zonas tengan un ligero margen entre ellas
        for (int i = 0; i < adjustedZones.Count - 1; i++)
        {
            var current = adjustedZones[i];
            var next = adjustedZones[i + 1];

            double currentUpper = current.Target * (1.0 + current.MarginPercent / 200.0);
            double nextLower = next.Target * (1.0 - next.MarginPercent / 200.0);
            double currentGap = nextLower - currentUpper;
            double desiredGap = (current.Target + next.Target) / 2.0 * (gapPercent / 100.0);

            if (Math.Abs(currentGap - desiredGap) > 0.0001)
            {
                double gapPoint = (currentUpper + nextLower) / 2.0;
                double adjustedCurrentUpper = gapPoint - desiredGap / 2.0;
                double adjustedNextLower = gapPoint + desiredGap / 2.0;

                double newCurrentMargin = ((adjustedCurrentUpper / current.Target) - 1.0) * 200.0;
                newCurrentMargin = Math.Max(1.0, newCurrentMargin);

                double newNextMargin = (1.0 - (adjustedNextLower / next.Target)) * 200.0;
                newNextMargin = Math.Max(1.0, newNextMargin);

                adjustedZones[i] = (current.Target, newCurrentMargin, current.BaseProbability, current.ZoneType);
                adjustedZones[i + 1] = (next.Target, newNextMargin, next.BaseProbability, next.ZoneType);

                double verifyCurrentUpper = current.Target * (1.0 + newCurrentMargin / 200.0);
                double verifyNextLower = next.Target * (1.0 - newNextMargin / 200.0);
                double verifyGap = verifyNextLower - verifyCurrentUpper;

                if (Math.Abs(verifyGap - desiredGap) > 0.0001)
                {
                    double targetGapPoint = verifyCurrentUpper + desiredGap / 2.0;
                    double denominator = 1.0 - (newNextMargin / 200.0);
                    if (denominator > 0.001)
                    {
                        double adjustedNextTarget = (verifyCurrentUpper + desiredGap) / denominator;
                        adjustedZones[i + 1] = (adjustedNextTarget, newNextMargin, next.BaseProbability, next.ZoneType);
                    }
                }
            }
        }

        return adjustedZones;
    }
}
