// UpdaterService.cs
using Newtonsoft.Json.Linq;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using Microsoft.EntityFrameworkCore;
using BetsTrading_Service.Locale;
using System.Globalization;
using System.Text.Json;
using EFCore.BulkExtensions;
using static BetsTrading_Service.Models.TwelveDataParser;
using Microsoft.Extensions.Options;

namespace BetsTrading_Service.Services
{
  public class UpdaterService(AppDbContext dbContext, ICustomLogger customLogger,
    FirebaseNotificationService firebaseNotificationService, IOptions<OddsAdjusterOptions> options)
  {
    private int currentKeyIndex = 0;
    private static readonly string[] TWELVE_DATA_KEYS = Enumerable.Range(0, 11).Select(i => Environment.GetEnvironmentVariable($"TWELVE_DATA_KEY{i}") ?? "").ToArray() ?? [];
    private readonly string MARKETSTACK_KEY = Environment.GetEnvironmentVariable("MARKETSTACK_API_KEY") ?? "";
    private readonly decimal FIXED_EUR_USD = 1.16m;
    readonly Random random = new();
    private const int PRICE_BET_WIN_PRICE = 50000;
    private readonly FirebaseNotificationService _firebaseNotificationService = firebaseNotificationService;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ICustomLogger _logger = customLogger;
    private readonly IOptions<OddsAdjusterOptions> _options = options;
    private static readonly HttpClient client = new();

    public async Task UpdateAssetsAsync(IServiceScopeFactory scopeFactory, bool marketHours, CancellationToken ct = default)
    {
      _logger.Log.Debug("[UpdaterService] :: UpdateAssetsAsync() called! {Mode}", marketHours ? "Mode market hours" : "Continuous mode");

      if (TWELVE_DATA_KEYS is null || TWELVE_DATA_KEYS.Length == 0 || TWELVE_DATA_KEYS.All(string.IsNullOrWhiteSpace))
      {
        _logger.Log.Error("[UpdaterService] :: TWELVE DATA KEYS not set in environment variables!");
        return;
      }

      using var scope = scopeFactory.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

      var query = !marketHours
        ? _dbContext.FinancialAssets.AsNoTracking().Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
        : _dbContext.FinancialAssets.AsNoTracking();

      var eurUsdAsset = _dbContext.AssetCandles
          .AsNoTracking()
          .Where(ac => ac.AssetId == 223) // EURUSD FOREX
          .OrderByDescending(ac => ac.DateTime)
          .FirstOrDefault();

      var eurToUsd = eurUsdAsset != null ? eurUsdAsset.Close : FIXED_EUR_USD;

      var selectedAssets = await query.ToListAsync(ct);

      if (selectedAssets.Count == 0)
      {
        _logger.Log.Error("[UpdaterService] :: ZERO assets found! CHECK DATABASE");
        return;
      }

      using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

      const string interval = "1h";
      const string outputsize = "1000";
      const string desiredQuote = "EUR";

      int keyIndex = 0;
      int callsWithThisKey = 0;

      string CurrentKey() => TWELVE_DATA_KEYS[keyIndex] ?? string.Empty;

      void NextKeyLocal()
      {
        keyIndex = (keyIndex + 1) % TWELVE_DATA_KEYS.Length;
        callsWithThisKey = 0;
        _logger.Log.Debug("[UpdaterService] :: Switching to next TwelveDataKey (index {Index})", keyIndex);
        if (string.IsNullOrWhiteSpace(CurrentKey()))
          _logger.Log.Error("[UpdaterService] :: Current TwelveDataKey is EMPTY, check environment variables!");
      }

      foreach (var asset in selectedAssets)
      {
        ct.ThrowIfCancellationRequested();

        if (callsWithThisKey == 8)
        {
          var wrapped = (keyIndex + 1) % TWELVE_DATA_KEYS.Length == 0;
          NextKeyLocal();
          if (wrapped)
          {
            _logger.Log.Debug("[UpdaterService] :: Sleeping 45 seconds to bypass rate limit");
            await Task.Delay(TimeSpan.FromSeconds(45), ct);
          }
        }

        var symbol = (asset.ticker ?? string.Empty).Split('.')[0].Trim();
        if (string.IsNullOrWhiteSpace(symbol))
        {
          _logger.Log.Warning("[UpdaterService] :: Asset {Id} has empty ticker, skipping", asset.id);
          continue;
        }

        // Obtener la última fecha de la tabla EUR antes de hacer la llamada a la API
        // Solo consultamos AssetCandles porque es la única tabla que se alimenta directamente de la API
        // Las velas USD se generan después multiplicando las EUR por el tipo de cambio
        var lastDate = await db.AssetCandles
            .Where(c => c.AssetId == asset.id && c.Interval == interval)
            .MaxAsync(c => (DateTime?)c.DateTime, ct) ?? DateTime.MinValue;

        // Construir la URL con start_date si hay una última fecha válida
        string baseUrl = asset.group == "Cryptos"
            ? $"https://api.twelvedata.com/time_series?symbol={symbol}/{desiredQuote}&interval={interval}&timezone=UTC&apikey={CurrentKey()}"
            : $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={interval}&timezone=UTC&apikey={CurrentKey()}";

        string url;
        if (lastDate != DateTime.MinValue)
        {
          // Formato ISO 8601: 2006-01-02T15:04:05
          string startDateParam = lastDate.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
          url = $"{baseUrl}&start_date={startDateParam}";
          _logger.Log.Debug("[UpdaterService] :: Using start_date={StartDate} for {Symbol}", startDateParam, symbol);
        }
        else
        {
          // Si no hay última fecha, usar outputsize para obtener datos históricos
          url = $"{baseUrl}&outputsize={outputsize}";
          _logger.Log.Debug("[UpdaterService] :: No previous candles found for {Symbol}, using outputsize={OutputSize}", symbol, outputsize);
        }

        HttpResponseMessage resp;
        try
        {
          resp = await httpClient.GetAsync(url, ct);
          callsWithThisKey++;
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[UpdaterService] :: HTTP error fetching {Symbol}", symbol);
          callsWithThisKey++;
          continue;
        }

        if (!resp.IsSuccessStatusCode)
        {
          _logger.Log.Error("[UpdaterService] :: TwelveData: Failed for {Symbol}. HTTP {Code}", symbol, resp.StatusCode);
          continue;
        }

        string json;
        try
        {
          json = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[UpdaterService] :: Error reading content for {Symbol}", symbol);
          continue;
        }

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
          _logger.Log.Error(ex, "[UpdaterService] :: JSON parse error for {Symbol}", symbol);
          continue;
        }

        if (parsed == null || parsed.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
        {
          _logger.Log.Warning("[UpdaterService] :: API status not ok for {Symbol}. Raw: {Raw}", symbol, json);
          continue;
        }

        if (parsed.Values == null || parsed.Values.Count == 0)
        {
          _logger.Log.Warning("[UpdaterService] :: No market data for {Symbol}", symbol);
          continue;
        }

        var exchange = parsed.Meta?.Exchange ?? "Unknown";

        var newCandles = new List<AssetCandle>();

        foreach (var v in parsed.Values)
        {
          try
          {
            var dtRaw = DateTime.Parse(v.Datetime!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            var dt = new DateTime(dtRaw.Year, dtRaw.Month, dtRaw.Day, dtRaw.Hour, 0, 0, DateTimeKind.Utc);

            if (dt <= lastDate)
              continue;

            newCandles.Add(new AssetCandle
            {
              AssetId = asset.id,
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
            _logger.Log.Error(ex, "[UpdaterService] :: Parse error for {Symbol} at {Date}", symbol, v.Datetime);
          }
        }

        if (newCandles.Count == 0)
        {
          _logger.Log.Debug("[UpdaterService] :: No new candles for {Symbol}", symbol);
          continue;
        }

        try
        {
          await using var transaction = await db.Database.BeginTransactionAsync(ct);

          var bulkConfig = new BulkConfig
          {
            UpdateByProperties = new List<string> { "AssetId", "Exchange", "Interval", "DateTime" },
            SetOutputIdentity = false,
            UseTempDB = true
          };

          await db.BulkInsertOrUpdateAsync(newCandles, bulkConfig, cancellationToken: ct);

          if (!string.Equals(asset.group, "Forex", StringComparison.OrdinalIgnoreCase))
          {
            var usdCandles = new List<AssetCandleUSD>(newCandles.Count);
            foreach (var c in newCandles)
            {
              usdCandles.Add(new AssetCandleUSD
              {
                
                AssetId = c.AssetId,
                Exchange = c.Exchange,
                Interval = c.Interval,
                DateTime = c.DateTime,
                Open = c.Open * eurToUsd,
                High = c.High * eurToUsd,
                Low = c.Low * eurToUsd,
                Close = c.Close * eurToUsd
              });
            }

            if (usdCandles.Count > 0)
            {
              var bulkUsdConfig = new BulkConfig
              {
                UpdateByProperties = new List<string> { "AssetId", "Exchange", "Interval", "DateTime" },
                SetOutputIdentity = false,
                UseTempDB = true
              };

              await db.BulkInsertOrUpdateAsync(usdCandles, bulkUsdConfig, cancellationToken: ct);
            }
          }

          var lastClose = newCandles
              .OrderByDescending(c => c.DateTime)
              .First()
              .Close;

          var lastCloseUsd = lastClose * eurToUsd;

          await db.FinancialAssets
              .Where(f => f.id == asset.id)
              .ExecuteUpdateAsync(s => s
                  .SetProperty(f => f.current_eur, _ => (double)lastClose)
                  .SetProperty(f => f.current_usd, _ => (double)lastCloseUsd),
                  ct);

          await transaction.CommitAsync(ct);

          _logger.Log.Debug("[UpdaterService] :: Saved {Count} new candles for {Symbol}", newCandles.Count, symbol);
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[UpdaterService] :: Error saving candles for {Symbol}", symbol);
        }
      }

      _logger.Log.Debug("[UpdaterService] :: UpdateAssetsAsync() completed successfully! ({Mode})", marketHours ? "Mode market hours" : "Continuous mode");
    }

    public async Task CreateBetZones(bool marketHoursMode)
    {
      _logger.Log.Debug("[UpdaterService] :: CreateBetZones() called with mode market-Hours = {0}", marketHoursMode.ToString());
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var query = !marketHoursMode ?
               _dbContext.FinancialAssets.Where(a => (a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex"))
             : _dbContext.FinancialAssets;

        var financialAssets = await query.ToListAsync();
        
        _logger.Log.Debug("[UpdaterService] :: Found {Count} financial assets to process", financialAssets.Count);
        
        if (financialAssets.Count == 0)
        {
          _logger.Log.Warning("[UpdaterService] :: No financial assets found! Query returned empty list.");
          await transaction.RollbackAsync();
          return;
        }

        int assetsProcessed = 0;
        foreach (var currentAsset in financialAssets)
        {
          assetsProcessed++;
          _logger.Log.Debug("[UpdaterService] :: Processing asset {Index}/{Total}: {Ticker} (ID: {Id})", 
            assetsProcessed, financialAssets.Count, currentAsset.ticker, currentAsset.id);
          var now = DateTime.UtcNow;

          // DESACTIVAR ZONAS EXISTENTES ANTES DE CREAR NUEVAS
          // Esto evita acumulación de zonas solapadas cuando el servicio se ejecuta periódicamente
          var existingZones = await _dbContext.BetZones
            .Where(bz => bz.ticker == currentAsset.ticker && bz.active)
            .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));

          var existingZonesUSD = await _dbContext.BetZonesUSD
            .Where(bz => bz.ticker == currentAsset.ticker && bz.active)
            .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));

          if (existingZones > 0 || existingZonesUSD > 0)
          {
            _logger.Log.Debug("[UpdaterService] :: Deactivated {Count} existing zones (EUR: {EurCount}, USD: {UsdCount}) for {Ticker} before creating new ones", 
              existingZones + existingZonesUSD, existingZones, existingZonesUSD, currentAsset.ticker);
          }
          
          _logger.Log.Debug("[UpdaterService] :: Starting zone creation process for {Ticker}", currentAsset.ticker);

          // Definir múltiples columnas temporales para cada timeframe
          // Cada timeframe tendrá al menos 3 períodos temporales de diferentes tamaños
          var horizons = new Dictionary<int, List<(DateTime Start, DateTime End)>>();

          // Timeframe 1h: 3 períodos cortos (3 columnas)
          horizons[1] = new List<(DateTime Start, DateTime End)>
          {
            (now.AddHours(2), now.AddHours(5)),   // Muy corto: 3 horas
            (now.AddHours(5), now.AddHours(10)),  // Corto: 5 horas
            (now.AddHours(10), now.AddHours(18))  // Medio: 8 horas
          };

          // Timeframe 2h: 3 períodos (3 columnas máximo)
          horizons[2] = new List<(DateTime Start, DateTime End)>
          {
            (now.AddHours(2), now.AddHours(8)),   // Corto: 6 horas
            (now.AddHours(8), now.AddHours(16)),  // Medio: 8 horas
            (now.AddHours(16), now.AddHours(28))  // Largo: 12 horas
          };

          // Timeframe 4h: 3 períodos (3 columnas máximo)
          horizons[4] = new List<(DateTime Start, DateTime End)>
          {
            (now.AddHours(2), now.AddHours(10)),   // Corto: 8 horas
            (now.AddHours(10), now.AddHours(22)),  // Medio: 12 horas
            (now.AddHours(22), now.AddHours(40))   // Largo: 18 horas
          };

          // Timeframe 24h: 3 períodos (3 columnas máximo)
          horizons[24] = new List<(DateTime Start, DateTime End)>
          {
            (now.AddHours(2), now.AddHours(26)),   // Corto: 24 horas (1 día)
            (now.AddHours(26), now.AddHours(74)),  // Medio: 48 horas (2 días)
            (now.AddHours(74), now.AddHours(146))  // Largo: 72 horas (3 días)
          };

          _logger.Log.Debug("[UpdaterService] :: Starting to process {TimeframeCount} timeframes for {Ticker}", 
            horizons.Count, currentAsset.ticker);
          
          foreach (var timeframeEntry in horizons)
          {
            int timeframe = timeframeEntry.Key;
            var timePeriods = timeframeEntry.Value;
            
            _logger.Log.Debug("[UpdaterService] :: Processing timeframe {Timeframe} for {Ticker}", 
              timeframe, currentAsset.ticker);
            
            // Las zonas existentes ya fueron desactivadas arriba para evitar solapamientos
            // Solo verificar que tengamos datos suficientes

            // Obtener más candles para análisis técnico robusto
            _logger.Log.Debug("[UpdaterService] :: Fetching candles for {Ticker} (AssetId: {Id})", 
              currentAsset.ticker, currentAsset.id);
            
            var candles = await _dbContext.AssetCandles
                .Where(c => c.AssetId == currentAsset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Take(100) // Más datos para análisis técnico
                .ToListAsync();
            
            _logger.Log.Debug("[UpdaterService] :: Retrieved {Count} candles for {Ticker} timeframe {Timeframe}", 
              candles.Count, currentAsset.ticker, timeframe);

            if (candles.Count < 50)
            {
              _logger.Log.Warning("[UpdaterService] :: Insufficient candles for {Ticker} timeframe {Timeframe}. Found: {Count}, Required: 50", 
                currentAsset.ticker, timeframe, candles.Count);
              continue; // Mínimo 50 candles para análisis confiable
            }
            
            _logger.Log.Debug("[UpdaterService] :: Processing {Ticker} timeframe {Timeframe} with {CandleCount} candles", 
              currentAsset.ticker, timeframe, candles.Count);

            // Preparar datos
            var closes = candles.Select(c => (double)c.Close).Reverse().ToList(); // Invertir para orden cronológico
            var highs = candles.Select(c => (double)c.High).Reverse().ToList();
            var lows = candles.Select(c => (double)c.Low).Reverse().ToList();
            double currentPrice = closes.Last(); // Precio más reciente
            
            if (currentPrice <= 0 || double.IsNaN(currentPrice) || double.IsInfinity(currentPrice))
            {
              _logger.Log.Error("[UpdaterService] :: Invalid current price for {Ticker} timeframe {Timeframe}: {Price}", 
                currentAsset.ticker, timeframe, currentPrice);
              continue;
            }

            // Análisis exhaustivo de las últimas 10xT velas para calcular márgenes máximos
            int candlesToAnalyze = 30 * timeframe; // 10xT velas de 1h
            var timeframeCandles = candles.Take(Math.Min(candlesToAnalyze, candles.Count)).Reverse().ToList();
            double maxVariationPercent = CalculateMaxVariationForTimeframe(timeframeCandles, currentPrice);

            // Calcular retornos logarítmicos
            var returns = CalculateLogReturns(closes);

            // Calcular volatilidad EWMA (más reactiva que stdDev simple)
            double volatility = CalculateEWMAVolatility(returns);
            if (volatility == 0 || double.IsNaN(volatility) || double.IsInfinity(volatility))
            {
              // Fallback a desviación estándar si EWMA falla
              double avgClose = closes.Average();
              volatility = Math.Sqrt(closes.Average(c => Math.Pow(c - avgClose, 2))) / avgClose;
              _logger.Log.Debug("[UpdaterService] :: Using fallback volatility calculation for {Ticker} timeframe {Timeframe}: {Vol}", 
                currentAsset.ticker, timeframe, volatility);
            }
            
            if (volatility <= 0 || double.IsNaN(volatility) || double.IsInfinity(volatility))
            {
              _logger.Log.Warning("[UpdaterService] :: Invalid volatility for {Ticker} timeframe {Timeframe}: {Vol}. Skipping.", 
                currentAsset.ticker, timeframe, volatility);
              continue;
            }

            // Calcular drift (tendencia)
            double drift = CalculateDrift(returns);

            // Indicadores técnicos
            double rsi = CalculateRSI(closes);
            var bollinger = CalculateBollingerBands(closes);

            // Detectar soportes y resistencias
            List<double> supports;
            List<double> resistances;
            try
            {
              var result = DetectSupportResistance(highs, lows, closes);
              supports = result.Supports;
              resistances = result.Resistances;
              _logger.Log.Debug("[UpdaterService] :: Detected {SupportCount} supports and {ResistanceCount} resistances for {Ticker}", 
                supports.Count, resistances.Count, currentAsset.ticker);
            }
            catch (Exception ex)
            {
              _logger.Log.Error(ex, "[UpdaterService] :: Error detecting support/resistance for {Ticker}", currentAsset.ticker);
              supports = new List<double>();
              resistances = new List<double>();
            }

            // Obtener tipo de cambio EUR/USD
            var eurUsdAsset = _dbContext.AssetCandles
              .AsNoTracking()
              .Where(ac => ac.AssetId == 223) // EURUSD FOREX
              .OrderByDescending(ac => ac.DateTime)
              .FirstOrDefault();

            var eurToUsd = eurUsdAsset != null ? eurUsdAsset.Close : FIXED_EUR_USD;

            // Generar zonas para cada período temporal (columna)
            // Máximo 3 períodos (3 columnas) × 3 zonas por período (3 filas) = 9 zonas totales
            int totalZonesCreated = 0;
            int maxPeriods = Math.Min(3, timePeriods.Count); // Limitar a máximo 3 períodos
            
            _logger.Log.Debug("[UpdaterService] :: Generating zones for {Ticker} timeframe {Timeframe} with {PeriodCount} periods", 
              currentAsset.ticker, timeframe, maxPeriods);
            
            for (int periodIndex = 0; periodIndex < maxPeriods; periodIndex++)
            {
              var period = timePeriods[periodIndex];
              double timeToExpiry = (period.End - now).TotalHours;
              
              // Generar exactamente 3 zonas por período (3 filas) para tener 9 zonas totales (3x3)
              int zonesPerPeriod = 3;
              
              // Generar zonas para este período con márgenes máximos basados en variación real
              _logger.Log.Debug("[UpdaterService] :: Calling GenerateIntelligentZones for {Ticker} timeframe {Timeframe} period {PeriodIndex}. Price: {Price}, Volatility: {Vol}, TimeToExpiry: {Hours}h", 
                currentAsset.ticker, timeframe, periodIndex, currentPrice, volatility, timeToExpiry);
              
              List<(double Target, double Margin, double BaseProbability, string ZoneType)> zones;
              try
              {
                zones = GenerateIntelligentZones(
                  currentPrice, supports, resistances, volatility, 
                  timeToExpiry, rsi, bollinger, drift, zoneCount: zonesPerPeriod,
                  maxVariationPercent: maxVariationPercent);
                
                _logger.Log.Debug("[UpdaterService] :: GenerateIntelligentZones returned {Count} zones for {Ticker} timeframe {Timeframe} period {PeriodIndex}", 
                  zones?.Count ?? 0, currentAsset.ticker, timeframe, periodIndex);
              }
              catch (Exception ex)
              {
                _logger.Log.Error(ex, "[UpdaterService] :: Error in GenerateIntelligentZones for {Ticker} timeframe {Timeframe} period {PeriodIndex}", 
                  currentAsset.ticker, timeframe, periodIndex);
                zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
              }

              // Validar que se generaron zonas
              if (zones == null || zones.Count == 0)
              {
                _logger.Log.Warning("[UpdaterService] :: No zones generated for {Ticker} timeframe {Timeframe} period {PeriodIndex}. Price: {Price}, Volatility: {Vol}", 
                  currentAsset.ticker, timeframe, periodIndex, currentPrice, volatility);
                continue; // Saltar este período si no se generaron zonas
              }

              // Ajustar probabilidades según distancia temporal (períodos más largos = menos probable)
              double timeAdjustmentFactor = 1.0 - (periodIndex * 0.08); // Reducir 8% por cada período más lejano
              timeAdjustmentFactor = Math.Max(0.60, timeAdjustmentFactor); // Mínimo 60% de la probabilidad original

              // Ajustar márgenes porcentuales para que las zonas se toquen en sus extremos
              // Primero convertir márgenes absolutos a porcentuales
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
                  _logger.Log.Warning("[UpdaterService] :: Invalid zone data for {Ticker}: Target={Target}, MarginPercent={Margin}", 
                    currentAsset.ticker, zone.Target, zone.MarginPercent);
                  continue;
                }

                double adjustedProb = zone.BaseProbability * timeAdjustmentFactor;
                double odds = ProbabilityToOdds(adjustedProb, 0.95);

                _dbContext.BetZones.Add(new BetZone(
                    currentAsset.ticker!,
                    zone.Target,
                    Math.Round(zone.MarginPercent, 1),
                    period.Start,
                    period.End,
                    Math.Round(odds, 2),
                    betTypeCounter % 2, // Alternar entre 0 y 1
                    timeframe
                ));
                betTypeCounter++;
              }

              // Crear zonas USD para este período
              betTypeCounter = 0;
              foreach (var zone in zonesWithPercentMargins)
              {
                if (zone.Target <= 0 || zone.MarginPercent <= 0)
                {
                  continue; // Ya se logueó arriba
                }

                double adjustedProb = zone.BaseProbability * timeAdjustmentFactor;
                double odds = ProbabilityToOdds(adjustedProb, 0.95);

                _dbContext.BetZonesUSD.Add(new BetZoneUSD(
                    currentAsset.ticker!,
                    zone.Target * (double)eurToUsd,
                    Math.Round(zone.MarginPercent, 1),
                    period.Start,
                    period.End,
                    Math.Round(odds, 2),
                    betTypeCounter % 2,
                    timeframe
                ));
                betTypeCounter++;
              }

              totalZonesCreated += zonesWithPercentMargins.Count;
              
              _logger.Log.Debug("[UpdaterService] :: Created {Count} zones for period {PeriodIndex} ({Start} to {End}) for {Ticker} timeframe {Timeframe}", 
                zonesWithPercentMargins.Count, periodIndex, period.Start, period.End, currentAsset.ticker, timeframe);
            }

            _logger.Log.Debug("[UpdaterService] :: Created TOTAL {Count} zones across {Periods} time periods for {Ticker} timeframe {Timeframe}", 
              totalZonesCreated, timePeriods.Count, currentAsset.ticker, timeframe);
          }
        }
        
        var totalZonesBeforeSave = _dbContext.BetZones.Local.Count + _dbContext.BetZonesUSD.Local.Count;
        _logger.Log.Debug("[UpdaterService] :: About to save {Count} zones to database", totalZonesBeforeSave);

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.Log.Information("[UpdaterService] :: CreateBetZones() completed successfully with technical analysis. ({0})", 
          (marketHoursMode ? "Mode market hours" : "Continuous mode"));
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[UpdaterService] :: CreateBetZones() error. Message: {Message}, StackTrace: {StackTrace}", 
          ex.Message, ex.StackTrace);
        try
        {
          await transaction.RollbackAsync();
          _logger.Log.Debug("[UpdaterService] :: Transaction rolled back successfully");
        }
        catch (Exception rollbackEx)
        {
          _logger.Log.Error(rollbackEx, "[UpdaterService] :: Error rolling back transaction");
        }
      }
    }

    // ========== ANÁLISIS TÉCNICO Y MODELOS FINANCIEROS ==========

    /// <summary>
    /// Calcula la volatilidad EWMA (Exponentially Weighted Moving Average)
    /// Más reactiva que la desviación estándar simple
    /// </summary>
    private static double CalculateEWMAVolatility(List<double> returns, double lambda = 0.94)
    {
      if (returns.Count < 2) return 0.0;

      double variance = 0.0;
      double weightSum = 0.0;

      for (int i = returns.Count - 1; i >= 0; i--)
      {
        double weight = Math.Pow(lambda, returns.Count - 1 - i);
        variance += weight * returns[i] * returns[i];
        weightSum += weight;
      }

      return Math.Sqrt(variance / weightSum);
    }

    /// <summary>
    /// Calcula los retornos logarítmicos
    /// </summary>
    private static List<double> CalculateLogReturns(List<double> prices)
    {
      var returns = new List<double>();
      for (int i = 1; i < prices.Count; i++)
      {
        if (prices[i - 1] > 0)
          returns.Add(Math.Log(prices[i] / prices[i - 1]));
      }
      return returns;
    }

    /// <summary>
    /// Calcula el RSI (Relative Strength Index)
    /// </summary>
    private static double CalculateRSI(List<double> closes, int period = 14)
    {
      if (closes.Count < period + 1) return 50.0; // Neutral si no hay suficientes datos

      var gains = new List<double>();
      var losses = new List<double>();

      for (int i = closes.Count - period; i < closes.Count; i++)
      {
        double change = closes[i] - closes[i - 1];
        if (change > 0) gains.Add(change);
        else if (change < 0) losses.Add(Math.Abs(change));
      }

      double avgGain = gains.Count > 0 ? gains.Average() : 0.0;
      double avgLoss = losses.Count > 0 ? losses.Average() : 0.0;

      if (avgLoss == 0) return 100.0;
      double rs = avgGain / avgLoss;
      return 100.0 - (100.0 / (1.0 + rs));
    }

    /// <summary>
    /// Calcula las Bandas de Bollinger
    /// </summary>
    private static (double Upper, double Middle, double Lower) CalculateBollingerBands(List<double> closes, int period = 20, double numStdDev = 2.0)
    {
      if (closes.Count < period) period = closes.Count;

      var recentCloses = closes.Skip(Math.Max(0, closes.Count - period)).Take(period).ToList();
      double sma = recentCloses.Average();
      double variance = recentCloses.Average(c => Math.Pow(c - sma, 2));
      double stdDev = Math.Sqrt(variance);

      return (sma + numStdDev * stdDev, sma, sma - numStdDev * stdDev);
    }

    /// <summary>
    /// Detecta soportes y resistencias usando pivots locales
    /// </summary>
    private static (List<double> Supports, List<double> Resistances) DetectSupportResistance(
      List<double> highs, List<double> lows, List<double> closes, int lookback = 5)
    {
      var supports = new List<double>();
      var resistances = new List<double>();

      for (int i = lookback; i < closes.Count - lookback; i++)
      {
        // Detectar mínimos locales (soportes)
        bool isLocalMin = true;
        for (int j = i - lookback; j <= i + lookback; j++)
        {
          if (j != i && lows[j] < lows[i])
          {
            isLocalMin = false;
            break;
          }
        }
        if (isLocalMin && !supports.Contains(lows[i]))
          supports.Add(lows[i]);

        // Detectar máximos locales (resistencias)
        bool isLocalMax = true;
        for (int j = i - lookback; j <= i + lookback; j++)
        {
          if (j != i && highs[j] > highs[i])
          {
            isLocalMax = false;
            break;
          }
        }
        if (isLocalMax && !resistances.Contains(highs[i]))
          resistances.Add(highs[i]);
      }

      return (supports.OrderByDescending(s => s).Take(5).ToList(),
              resistances.OrderBy(r => r).Take(5).ToList());
    }

    /// <summary>
    /// Calcula la probabilidad de que el precio alcance un nivel usando movimiento browniano geométrico
    /// </summary>
    private static double CalculateReachProbability(
      double currentPrice, double targetPrice, double volatility, 
      double timeToExpiryHours, double drift = 0.0)
    {
      if (timeToExpiryHours <= 0) return currentPrice == targetPrice ? 1.0 : 0.0;
      if (currentPrice <= 0 || targetPrice <= 0) return 0.01;

      // Convertir tiempo a años para el modelo
      double timeToExpiry = timeToExpiryHours / (365.0 * 24.0);
      
      // Anualizar volatilidad (asumiendo que viene en términos horarios)
      double annualizedVol = volatility * Math.Sqrt(24.0 * 365.0);
      
      // Usar movimiento browniano geométrico: dS = μS dt + σS dW
      double logPriceRatio = Math.Log(targetPrice / currentPrice);
      double adjustedDrift = drift - 0.5 * annualizedVol * annualizedVol; // Ajuste de Itô
      
      // Probabilidad usando distribución normal
      double mean = adjustedDrift * timeToExpiry;
      double variance = annualizedVol * annualizedVol * timeToExpiry;
      double stdDev = Math.Sqrt(variance);

      if (stdDev == 0 || double.IsNaN(stdDev)) return Math.Abs(logPriceRatio) < 0.001 ? 0.5 : 0.01;

      // Calcular probabilidad usando función de distribución acumulada normal
      double z = (logPriceRatio - mean) / stdDev;
      
      // Aproximación de la CDF normal usando función de error
      double probability = 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
      
      // Asegurar que la probabilidad esté en un rango razonable
      return Math.Max(0.01, Math.Min(0.99, probability));
    }

    /// <summary>
    /// Función de error complementaria para aproximar la CDF normal
    /// </summary>
    private static double Erf(double x)
    {
      // Aproximación de Abramowitz y Stegun
      double a1 = 0.254829592;
      double a2 = -0.284496736;
      double a3 = 1.421413741;
      double a4 = -1.453152027;
      double a5 = 1.061405429;
      double p = 0.3275911;

      int sign = x < 0 ? -1 : 1;
      x = Math.Abs(x);

      double t = 1.0 / (1.0 + p * x);
      double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

      return sign * y;
    }

    /// <summary>
    /// Calcula el drift (tendencia) basado en regresión lineal de retornos
    /// </summary>
    private static double CalculateDrift(List<double> returns, double timeStep = 1.0)
    {
      if (returns.Count < 2) return 0.0;
      return returns.Average() / timeStep;
    }

    /// <summary>
    /// Convierte probabilidad a odds decimales
    /// </summary>
    private static double ProbabilityToOdds(double probability, double margin = 0.95)
    {
      if (probability <= 0) return 100.0; // Odds muy altas para probabilidad 0
      if (probability >= 1) return 1.01; // Odds mínimas para probabilidad 1
      
      double fairOdds = 1.0 / probability;
      return Math.Max(1.01, Math.Round(fairOdds * margin, 2));
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
      // Log de entrada (necesitamos acceso al logger, pero es static, así que usaremos System.Diagnostics.Debug o simplemente retornar temprano si hay problemas)
      var zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
      
      // Validación temprana
      if (currentPrice <= 0 || double.IsNaN(currentPrice) || double.IsInfinity(currentPrice))
      {
        return zones; // Retornar lista vacía si el precio es inválido
      }

      // Asegurar que la volatilidad tenga un mínimo razonable para generar zonas visibles
      double minVolatility = 0.01; // 1% mínimo
      double effectiveVolatility = Math.Max(volatility, minVolatility);

      // Definir porcentajes de distancia del precio actual para generar zonas VISUALMENTE DISTINTAS
      // Generar MUCHAS zonas distribuidas ampliamente
      var zonePercentages = new List<double>();
      
      if (zoneCount <= 3)
      {
        zonePercentages = zoneCount switch
        {
          2 => new List<double> { -0.025, 0.025 },
          3 => new List<double> { -0.04, 0.04 }, // 3 zonas: una abajo, una actual (ya se añade), una arriba
          _ => new List<double> { -0.04, 0.04 }
        };
      }
      else
      {
        // Para muchas zonas (6+), generar distribución amplia y variada
        zonePercentages.Add(0.0); // Zona actual siempre
        
        // Zonas por debajo: distribuidas desde -6% hasta -1% en incrementos de 0.75%
        for (double pct = -0.06; pct <= -0.01; pct += 0.0075)
        {
          zonePercentages.Add(pct);
        }
        
        // Zonas por arriba: distribuidas desde +1% hasta +6% en incrementos de 0.75%
        for (double pct = 0.01; pct <= 0.06; pct += 0.0075)
        {
          zonePercentages.Add(pct);
        }
        
        // Para timeframes largos, añadir zonas más extremas
        if (timeToExpiryHours > 12)
        {
          zonePercentages.AddRange(new[] { -0.09, -0.075, -0.10, 0.075, 0.09, 0.10 });
        }
      }

      // 1. ZONA ACTUAL (0% - precio actual) - siempre la primera, probabilidad muy alta
      // Margen mínimo garantizado: al menos 1% del precio para que sea visible
      // Para 3 zonas, usar un margen más pequeño para permitir que las otras zonas se ajusten
      double currentZoneMargin = zoneCount == 3 
        ? Math.Max(currentPrice * 0.0075, currentPrice * effectiveVolatility * 0.01) // Margen más pequeño para 3 zonas
        : Math.Max(currentPrice * 0.01, currentPrice * effectiveVolatility * 0.0125); // Margen normal para más zonas
      double currentProb = 0.88; // Alta probabilidad de quedarse en zona actual
      zones.Add((currentPrice, currentZoneMargin, currentProb, "current"));

      // 2. Generar zonas distribuidas VISUALMENTE en diferentes niveles
      // Priorizar niveles técnicos reales, pero asegurar distribución visual
      var usedPercentages = new HashSet<double> { 0.0 }; // Ya usamos 0%

      // Buscar soportes/resistencias y mapearlos a los porcentajes más cercanos
      var technicalLevels = new List<(double Price, double Percentage, string Type)>();
      
      // Añadir soportes detectados
      foreach (var support in supports.Where(s => s > 0 && s < currentPrice * 1.5))
      {
        double pct = (support - currentPrice) / currentPrice;
        if (pct >= -0.10 && pct <= 0.10) // Solo dentro de ±10%
          technicalLevels.Add((support, pct, "support"));
      }

      // Añadir resistencias detectadas
      foreach (var resistance in resistances.Where(r => r > 0 && r > currentPrice * 0.5))
      {
        double pct = (resistance - currentPrice) / currentPrice;
        if (pct >= -0.10 && pct <= 0.10)
          technicalLevels.Add((resistance, pct, "resistance"));
      }

      // Añadir Bollinger Bands
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

      // Para cada porcentaje objetivo, buscar el nivel técnico más cercano o usar el porcentaje directamente
      int maxZoneAttempts = zoneCount * 10; // Límite de intentos para evitar bucles infinitos
      int zoneAttempts = 0;
      foreach (var targetPct in zonePercentages.Where(p => p != 0.0).OrderBy(p => Math.Abs(p)))
      {
        if (zones.Count >= zoneCount) break; // Ya tenemos suficientes zonas
        if (zoneAttempts++ >= maxZoneAttempts)
        {
          break; // Protección contra demasiados intentos
        }

        double targetPrice;
        string zoneType;
        double margin;
        
        // Buscar si hay un nivel técnico cercano a este porcentaje (dentro de 0.75%)
        var closestTechnical = technicalLevels
          .Where(t => Math.Abs(t.Percentage - targetPct) < 0.0075)
          .OrderBy(t => Math.Abs(t.Percentage - targetPct))
          .FirstOrDefault();

        if (closestTechnical.Price > 0)
        {
          // Usar el nivel técnico real
          targetPrice = closestTechnical.Price;
          zoneType = closestTechnical.Type;
          // Margen mínimo garantizado: al menos 1% del precio objetivo
          double baseMargin = targetPrice * 0.01;
          double volatilityMargin = targetPrice * effectiveVolatility * 0.015;
          margin = Math.Max(baseMargin, volatilityMargin);
        }
        else
        {
          // Usar el porcentaje directamente para asegurar distribución visual
          targetPrice = currentPrice * (1.0 + targetPct);
          zoneType = targetPct < 0 ? "below" : "above";
          
          // Calcular margen basado en distancia: zonas más lejanas = márgenes más grandes
          double distanceFromCurrent = Math.Abs(targetPct);
          
          // Para 3 zonas, usar márgenes más pequeños para permitir que se toquen
          // Si tenemos análisis de variación, usar márgenes basados en la variación real
          double baseMinMarginPercent = zoneCount == 3 ? 0.0075 : 0.01; // 0.75% para 3 zonas, 1% para más
          double baseMaxMarginPercent = zoneCount == 3 ? 0.025 : 0.04; // 2.5% para 3 zonas, 4% para más
          
          // Ajustar márgenes máximos basados en la variación real del período 10xT
          double minMarginPercent = baseMinMarginPercent;
          double maxMarginPercent = baseMaxMarginPercent;
          
          if (maxVariationPercent > 0)
          {
            // Usar la variación máxima como referencia, pero con límites razonables
            // El margen máximo debe ser al menos la mitad de la variación máxima observada
            // pero no más del doble de la variación máxima para evitar márgenes excesivos
            double variationBasedMax = Math.Max(maxVariationPercent * 0.5, baseMaxMarginPercent);
            variationBasedMax = Math.Min(variationBasedMax, maxVariationPercent * 2.0); // No más del doble de la variación
            variationBasedMax = Math.Max(variationBasedMax, baseMaxMarginPercent); // Al menos el mínimo base
            
            maxMarginPercent = variationBasedMax;
            minMarginPercent = Math.Max(baseMinMarginPercent, maxMarginPercent * 0.3); // Mínimo 30% del máximo
          }
          
          // Escalar margen según distancia: desde minMarginPercent (cerca) hasta maxMarginPercent (lejos)
          double marginPercent = minMarginPercent + (distanceFromCurrent / 0.10) * (maxMarginPercent - minMarginPercent);
          marginPercent = Math.Min(maxMarginPercent, Math.Max(minMarginPercent, marginPercent));
          
          margin = targetPrice * marginPercent;
          
          // Asegurar que el margen nunca sea menor que un mínimo absoluto
          double absoluteMinMargin = targetPrice * (zoneCount == 3 ? 0.006 : 0.0075); // Mínimo más pequeño para 3 zonas
          margin = Math.Max(margin, absoluteMinMargin);
        }

        // Verificar que la zona no se solape con otras zonas existentes
        // Una zona se solapa si los rangos se superponen
        // El margen es absoluto, así que los límites son: target ± margin
        // Para zonas cercanas, permitimos que se toquen (serán ajustadas después por AdjustZonesToTouchPercent)
        bool overlaps = zones.Any(z =>
        {
          double zUpper = z.Target + z.Margin;
          double zLower = z.Target - z.Margin;
          double targetUpper = targetPrice + margin;
          double targetLower = targetPrice - margin;
          
          // Verificar solapamiento: si los rangos se superponen significativamente
          // Permitir que se toquen (serán ajustadas después), pero no solapamiento real
          double overlapAmount = Math.Min(targetUpper - zLower, zUpper - targetLower);
          // Para 3 zonas, ser más permisivo para permitir que se generen
          if (zoneCount == 3)
          {
            // Para 3 zonas, solo rechazar si hay solapamiento mayor al 20% del margen
            return overlapAmount > (Math.Min(margin, z.Margin) * 0.2);
          }
          else
          {
            // Para más zonas, rechazar cualquier solapamiento significativo
            return overlapAmount > (Math.Min(margin, z.Margin) * 0.1);
          }
        });
        
        if (overlaps)
          continue;

        // Calcular probabilidad real
        double prob = 0.5; // Valor por defecto
        try
        {
          prob = CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
        }
        catch
        {
          // Si falla el cálculo, usar probabilidad por defecto
          prob = 0.5;
        }
        
        // Ajustar probabilidad según indicadores técnicos
        if (targetPct < 0 && rsi < 30) prob *= 1.15; // Más probable bajar si RSI sobrevendido
        if (targetPct > 0 && rsi > 70) prob *= 0.85; // Menos probable subir si RSI sobrecomprado
        
        // Ajustar probabilidad según distancia (zonas más lejanas = menos probable)
        double distanceFactor = 1.0 - (Math.Abs(targetPct) * 0.3); // Reducir probabilidad según distancia
        prob *= Math.Max(0.5, distanceFactor);
        
        // Asegurar rango razonable de probabilidades
        prob = Math.Max(0.15, Math.Min(0.75, prob));
        
        // Asegurar que zonas menos probables tengan márgenes aún más grandes para ser visibles
        if (prob < 0.30) 
        {
          margin *= 1.3; // Aumentar margen para zonas de baja probabilidad
        }
        
        // Garantizar margen mínimo final: nunca menos del 1% del precio objetivo
        double finalMinMargin = targetPrice * 0.01;
        margin = Math.Max(margin, finalMinMargin);
        
        zones.Add((targetPrice, margin, prob, zoneType));
        usedPercentages.Add(targetPct);
      }

      // 3. Si aún no tenemos suficientes zonas, completar con zonas distribuidas uniformemente
      int maxIterations = 100; // Protección contra bucle infinito
      int iterations = 0;
      while (zones.Count < zoneCount && iterations < maxIterations)
      {
        iterations++;
        // Generar zonas en incrementos uniformes
        int missingZones = zoneCount - zones.Count;
        double step = 0.075 / (missingZones + 1); // Distribuir en ±7.5%
        
        int zonesAddedThisIteration = 0;
        for (int i = 1; i <= missingZones && zones.Count < zoneCount; i++)
        {
          // Alternar entre arriba y abajo
          bool isUp = i % 2 == 0;
          double pct = isUp ? step * i : -step * i;
          
          // Asegurar que no esté muy cerca de zonas existentes
          if (usedPercentages.Any(up => Math.Abs(up - pct) < 0.01))
            continue;
            
          double targetPrice = currentPrice * (1.0 + pct);
          
          // Calcular margen para esta zona antes de verificar solapamiento
          double distanceFromCurrent = Math.Abs(pct);
          double baseMinMarginPercent = 0.01;
          double baseMaxMarginPercent = 0.04;
          
          // Ajustar márgenes basados en variación real si está disponible
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
          
          // Verificar que no se solape con zonas existentes
          // El margen es absoluto, así que los límites son: target ± margin
          // Permitir que se toquen (serán ajustadas después por AdjustZonesToTouchPercent)
          bool overlaps = zones.Any(z =>
          {
            double zUpper = z.Target + z.Margin;
            double zLower = z.Target - z.Margin;
            double targetUpper = targetPrice + tempMargin;
            double targetLower = targetPrice - tempMargin;
            
            // Verificar solapamiento: si los rangos se superponen significativamente
            double gap = Math.Min(targetLower - zUpper, zLower - targetUpper);
            return gap < 0 && Math.Abs(gap) > (tempMargin * 0.1); // Solo rechazar si hay solapamiento significativo
          });
          
          if (overlaps)
            continue;

          double prob = 0.5; // Valor por defecto
          try
          {
            prob = CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
            prob = Math.Max(0.20, Math.Min(0.60, prob * (1.0 - Math.Abs(pct) * 0.4)));
          }
          catch
          {
            // Si falla el cálculo, usar probabilidad por defecto
            prob = 0.5;
          }
          
          // Usar el margen ya calculado (tempMargin) que ya verificó no solaparse
          double margin = tempMargin;
          
          zones.Add((targetPrice, margin, prob, isUp ? "above" : "below"));
          usedPercentages.Add(pct);
          zonesAddedThisIteration++;
        }
        
        // Si no se agregaron zonas en esta iteración, salir del bucle para evitar infinito
        if (zonesAddedThisIteration == 0)
        {
          break; // No se pueden generar más zonas, usar las que tenemos
        }
        
        // Si aún no tenemos suficientes después de varios intentos, usar las zonas que tenemos
        // Asegurar que al menos tengamos la zona actual
        if (zones.Count == 0)
        {
          // Si no hay zonas, crear al menos una zona en el precio actual
          double minMargin = currentPrice * 0.01;
          zones.Add((currentPrice, minMargin, 0.5, "current"));
        }
        
        // Si aún no tenemos suficientes, romper el bucle para evitar infinito
        if (zones.Count < zoneCount / 2 && zones.Count > 0) break;
      }

      // 4. Ordenar por precio (de menor a mayor) para mejor visualización
      var sortedZones = zones.OrderBy(z => z.Target).ToList();
      
      // Asegurar distribución balanceada: al menos una zona arriba y una abajo del precio actual
      var zonesBelow = sortedZones.Where(z => z.Target < currentPrice).ToList();
      var zonesAbove = sortedZones.Where(z => z.Target > currentPrice).ToList();
      var currentZone = sortedZones.FirstOrDefault(z => Math.Abs(z.Target - currentPrice) / currentPrice < 0.001);
      bool hasCurrentZone = currentZone.Target > 0 && !double.IsNaN(currentZone.Target);

      var finalZones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();
      
      // Si no hay suficientes zonas generadas, devolver las que tenemos
      if (sortedZones.Count <= zoneCount)
      {
        return sortedZones.Take(zoneCount).ToList();
      }
      
      // Seleccionar zonas según el número solicitado
      if (zoneCount == 2)
      {
        // Para 2 zonas: 1 abajo, 1 arriba (sin zona actual para maximizar separación)
        if (zonesBelow.Any())
          finalZones.Add(zonesBelow.OrderByDescending(z => z.Target).First());
        
        if (zonesAbove.Any())
          finalZones.Add(zonesAbove.OrderBy(z => z.Target).First());
        
        // Si no hay suficientes, añadir zona actual
        if (finalZones.Count < 2 && hasCurrentZone)
          finalZones.Add(currentZone);
      }
      else if (zoneCount == 3)
      {
        // Para 3 zonas: 1 abajo, 1 actual, 1 arriba
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
        // Para más zonas: distribución más amplia
        if (hasCurrentZone && zoneCount > 3)
          finalZones.Add(currentZone);
        
        // Calcular cuántas zonas necesitamos arriba y abajo
        int zonesNeeded = zoneCount - finalZones.Count;
        int zonesBelowCount = zonesNeeded / 2;
        int zonesAboveCount = zonesNeeded - zonesBelowCount;
        
        // Añadir zonas abajo
        finalZones.AddRange(zonesBelow.OrderByDescending(z => z.Target).Take(zonesBelowCount));
        
        // Añadir zonas arriba
        finalZones.AddRange(zonesAbove.OrderBy(z => z.Target).Take(zonesAboveCount));
        
        // Si aún no tenemos suficientes, completar con las mejores restantes
        var remaining = sortedZones
          .Where(z => !finalZones.Any(fz => Math.Abs(fz.Target - z.Target) / currentPrice < 0.001))
          .OrderByDescending(z => z.BaseProbability)
          .Take(zoneCount - finalZones.Count);
        
        finalZones.AddRange(remaining);
      }
      
      // Asegurar que siempre devolvamos al menos una zona
      if (finalZones.Count == 0 && sortedZones.Count > 0)
      {
        finalZones.Add(sortedZones.First());
      }
      
      // Ordenar por precio para visualización y tomar exactamente zoneCount
      return finalZones.OrderBy(z => z.Target).Take(Math.Max(1, zoneCount)).ToList();
    }

    /// <summary>
    /// Ajusta los márgenes porcentuales y precios objetivo de las zonas para que se toquen en sus extremos sin solaparse.
    /// Las zonas verdes (superiores) tendrán su límite inferior igual al límite superior de la zona amarilla (actual).
    /// Las zonas rojas (inferiores) tendrán su límite superior igual al límite inferior de la zona amarilla (actual).
    /// El margin es porcentual total, y los límites se calculan como:
    /// - upperBound = target + (target * margin / 200)
    /// - lowerBound = target - (target * margin / 200)
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
        // Si no hay zona actual, usar la zona más cercana al precio medio
        double avgTarget = zones.Average(z => z.Target);
        currentZone = zones.OrderBy(z => Math.Abs(z.Target - avgTarget)).First();
      }

      // Calcular límites de la zona amarilla
      double yellowUpperBound = currentZone.Target * (1.0 + currentZone.MarginPercent / 200.0);
      double yellowLowerBound = currentZone.Target * (1.0 - currentZone.MarginPercent / 200.0);

      // Separar zonas en rojas (inferiores), amarilla (actual) y verdes (superiores)
      var redZones = zones.Where(z => (z.Target < currentZone.Target || z.ZoneType == "below") && 
                                      !(z.ZoneType == "current" || Math.Abs(z.Target - currentZone.Target) < 0.0001))
                          .OrderByDescending(z => z.Target).ToList(); // Ordenar de mayor a menor (más cerca de amarilla primero)
      var greenZones = zones.Where(z => (z.Target > currentZone.Target || z.ZoneType == "above") && 
                                        !(z.ZoneType == "current" || Math.Abs(z.Target - currentZone.Target) < 0.0001))
                            .OrderBy(z => z.Target).ToList(); // Ordenar de menor a mayor (más cerca de amarilla primero)

      var adjustedZones = new List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)>();
      
      // Ajustar zonas rojas (inferiores) - deben tener un ligero margen con la zona amarilla y entre sí
      const double gapPercent = 0.15; // 0.15% de margen entre zonas
      double currentLowerBound = yellowLowerBound;
      foreach (var zone in redZones)
      {
        // El límite superior de esta zona roja debe tener un pequeño gap con el límite inferior de la zona anterior (o amarilla)
        // redUpperBound = redTarget * (1 + redMargin / 200) = currentLowerBound * (1 - gapPercent / 100)
        // redTarget = (currentLowerBound * (1 - gapPercent / 100)) / (1 + redMargin / 200)
        double gapAdjustedBound = currentLowerBound * (1.0 - gapPercent / 100.0);
        double denominator = 1.0 + (zone.MarginPercent / 200.0);
        double adjustedTarget = gapAdjustedBound / denominator;
        double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent);
        
        adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
        
        // Calcular el límite inferior de esta zona para la siguiente iteración (con gap)
        currentLowerBound = adjustedTarget * (1.0 - adjustedMarginPercent / 200.0) * (1.0 - gapPercent / 100.0);
      }
      
      // Añadir zona amarilla (actual) - mantener como está
      adjustedZones.Add((currentZone.Target, Math.Max(1.0, currentZone.MarginPercent), currentZone.BaseProbability, currentZone.ZoneType));
      
      // Ajustar zonas verdes (superiores) - deben tener un ligero margen con la zona amarilla y entre sí
      double currentUpperBound = yellowUpperBound;
      foreach (var zone in greenZones)
      {
        // El límite inferior de esta zona verde debe tener un pequeño gap con el límite superior de la zona anterior (o amarilla)
        // greenLowerBound = greenTarget * (1 - greenMargin / 200) = currentUpperBound * (1 + gapPercent / 100)
        // greenTarget = (currentUpperBound * (1 + gapPercent / 100)) / (1 - greenMargin / 200)
        double gapAdjustedBound = currentUpperBound * (1.0 + gapPercent / 100.0);
        double denominator = 1.0 - (zone.MarginPercent / 200.0);
        if (denominator > 0.001) // Evitar división por cero
        {
          double adjustedTarget = gapAdjustedBound / denominator;
          double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent);
          
          adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
          
          // Calcular el límite superior de esta zona para la siguiente iteración (con gap)
          currentUpperBound = adjustedTarget * (1.0 + adjustedMarginPercent / 200.0) * (1.0 + gapPercent / 100.0);
        }
        else
        {
          // Si el margen es muy grande, ajustar el margen primero
          double adjustedMarginPercent = Math.Max(1.0, zone.MarginPercent * 0.5);
          denominator = 1.0 - (adjustedMarginPercent / 200.0);
          double adjustedTarget = gapAdjustedBound / denominator;
          
          adjustedZones.Add((adjustedTarget, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
          
          // Calcular el límite superior de esta zona para la siguiente iteración (con gap)
          currentUpperBound = adjustedTarget * (1.0 + adjustedMarginPercent / 200.0) * (1.0 + gapPercent / 100.0);
        }
      }

      // Ordenar zonas ajustadas por precio (target) de menor a mayor
      adjustedZones = adjustedZones.OrderBy(z => z.Target).ToList();
      
      // Verificar y ajustar que todas las zonas tengan un ligero margen entre ellas
      for (int i = 0; i < adjustedZones.Count - 1; i++)
      {
        var current = adjustedZones[i];
        var next = adjustedZones[i + 1];
        
        double currentUpper = current.Target * (1.0 + current.MarginPercent / 200.0);
        double nextLower = next.Target * (1.0 - next.MarginPercent / 200.0);
        
        // Calcular el gap actual
        double currentGap = nextLower - currentUpper;
        double desiredGap = (current.Target + next.Target) / 2.0 * (gapPercent / 100.0);
        
        // Si el gap es muy diferente del deseado, ajustar para mantener un margen consistente
        if (Math.Abs(currentGap - desiredGap) > 0.0001)
        {
          // Calcular el punto medio con el gap deseado
          double gapPoint = (currentUpper + nextLower) / 2.0;
          double adjustedCurrentUpper = gapPoint - desiredGap / 2.0;
          double adjustedNextLower = gapPoint + desiredGap / 2.0;
          
          // Ajustar margen de la zona actual
          double newCurrentMargin = ((adjustedCurrentUpper / current.Target) - 1.0) * 200.0;
          newCurrentMargin = Math.Max(1.0, newCurrentMargin);
          
          // Ajustar margen de la zona siguiente
          double newNextMargin = (1.0 - (adjustedNextLower / next.Target)) * 200.0;
          newNextMargin = Math.Max(1.0, newNextMargin);
          
          adjustedZones[i] = (current.Target, newCurrentMargin, current.BaseProbability, current.ZoneType);
          adjustedZones[i + 1] = (next.Target, newNextMargin, next.BaseProbability, next.ZoneType);
          
          // Si aún no hay el gap correcto, ajustar el precio objetivo de la zona siguiente
          double verifyCurrentUpper = current.Target * (1.0 + newCurrentMargin / 200.0);
          double verifyNextLower = next.Target * (1.0 - newNextMargin / 200.0);
          double verifyGap = verifyNextLower - verifyCurrentUpper;
          
          if (Math.Abs(verifyGap - desiredGap) > 0.0001)
          {
            // Ajustar el precio objetivo para lograr el gap deseado
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

    /// <summary>
    /// Calcula la variación máxima porcentual en las últimas 10xT velas del timeframe
    /// Analiza el rango high-low de cada vela y la variación total del período
    /// </summary>
    private static double CalculateMaxVariationForTimeframe(List<AssetCandle> candles, double currentPrice)
    {
      if (candles == null || candles.Count == 0 || currentPrice <= 0)
        return 0.0;

      try
      {
        // Calcular variaciones individuales de cada vela (high-low range)
        var candleVariations = new List<double>();
        foreach (var candle in candles)
        {
          if (candle.Close > 0)
          {
            // Variación porcentual de la vela (high-low range)
            double candleRange = (double)(candle.High - candle.Low) / (double)candle.Close;
            candleVariations.Add(candleRange);
          }
        }

        if (candleVariations.Count == 0)
          return 0.0;

        // Variación máxima de una sola vela
        double maxSingleCandleVariation = candleVariations.Max();

        // Variación total del período (precio máximo - precio mínimo)
        double maxPrice = candles.Max(c => (double)c.High);
        double minPrice = candles.Min(c => (double)c.Low);
        double totalRangePercent = (maxPrice - minPrice) / currentPrice;

        // Variación promedio ponderada (más peso a variaciones recientes)
        double weightedAvgVariation = 0.0;
        double weightSum = 0.0;
        for (int i = 0; i < candleVariations.Count; i++)
        {
          // Peso exponencial: más reciente = más peso
          double weight = Math.Pow(1.1, candleVariations.Count - i);
          weightedAvgVariation += candleVariations[i] * weight;
          weightSum += weight;
        }
        weightedAvgVariation = weightSum > 0 ? weightedAvgVariation / weightSum : 0.0;

        // Calcular desviación estándar de variaciones para detectar volatilidad extrema
        double avgVariation = candleVariations.Average();
        double variance = candleVariations.Average(v => Math.Pow(v - avgVariation, 2));
        double stdDev = Math.Sqrt(variance);

        // Usar el máximo entre:
        // 1. Variación máxima de una vela individual
        // 2. Variación total del período (ajustada)
        // 3. Promedio ponderado + 1 desviación estándar (para capturar volatilidad)
        double variation1 = maxSingleCandleVariation * 100.0; // Convertir a porcentaje
        double variation2 = totalRangePercent * 100.0;
        double variation3 = (weightedAvgVariation + stdDev) * 100.0;

        // Retornar el máximo, pero con un límite razonable (no más del 20%)
        double maxVariation = Math.Max(Math.Max(variation1, variation2), variation3);
        return Math.Min(maxVariation, 20.0); // Límite máximo del 20%
      }
      catch
      {
        return 0.0;
      }
    }

    private static double CalculateLinearRegressionSlope(List<double> data)
    {
      int n = data.Count;
      double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

      for (int i = 0; i < n; i++)
      {
        sumX += i;
        sumY += data[i];
        sumXY += i * data[i];
        sumX2 += i * i;
      }

      double numerator = n * sumXY - sumX * sumY;
      double denominator = n * sumX2 - sumX * sumX;

      return denominator == 0 ? 0 : numerator / denominator;
    }

    public async Task SetInactiveBetZones()
    {
      _logger.Log.Debug("[UpdaterService] :: SetInactiveBetZones() called");

      var now = DateTime.UtcNow;

      var affected = await _dbContext.BetZones
        .Where(bz => bz.active && now >= bz.start_date)
        .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));

      var affectedUSD = await _dbContext.BetZonesUSD
        .Where(bz => bz.active && now >= bz.start_date)
        .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));


      _logger.Log.Debug("[UpdaterService] :: SetInactiveBetZones() -> {Count} deactivated zones", affected);
    }

    public async Task SetFinishedBets()
    {
      _logger.Log.Debug("[UpdaterService] :: SetFinishedBets() called");

      var betsZonesToCheck = await _dbContext.BetZones
          .Where(bz => DateTime.UtcNow >= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync();

      if (betsZonesToCheck.Count != 0)
      {
        var betsToMark = _dbContext.Bets
          .Where(b => betsZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToList();

        foreach (var currentBet in betsToMark)
        {
          currentBet.finished = true;
          _dbContext.Bets.Update(currentBet);
        }

        _dbContext.SaveChanges();
        _logger.Log.Debug("[UpdaterService] :: SetFinishedBets() ended succesfully!");
      }
      else
      {
        _logger.Log.Warning("[UpdaterService] :: SetFinishedBets() no bets to check!");
      }
    }

    public async Task SetFinishedUSDBets()
    {
      _logger.Log.Debug("[UpdaterService] :: SetFinishedUSDBets() called");

      var betsZonesToCheck = await _dbContext.BetZonesUSD
          .Where(bz => DateTime.UtcNow >= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync();

      if (betsZonesToCheck.Count != 0)
      {
        var betsToMark = _dbContext.Bets
          .Where(b => betsZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToList();

        foreach (var currentBet in betsToMark)
        {
          currentBet.finished = true;
          _dbContext.Bets.Update(currentBet);
        }

        _dbContext.SaveChanges();
        _logger.Log.Debug("[UpdaterService] :: SetFinishedUSDBets() ended succesfully!");
      }
      else
      {
        _logger.Log.Warning("[UpdaterService] :: SetFinishedUSDBets() no bets to check!");
      }
    }

    public async Task CheckBets(bool marketHours)
    {
      _logger.Log.Debug("[UpdaterService] :: CheckBets() called with market hours mode = {0}", marketHours);

      var now = DateTime.UtcNow;

      var betZonesToCheck = (marketHours) ?
        await _dbContext.BetZones
          .Where(bz => now >= bz.start_date && now <= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync()
        :
        await _dbContext.BetZones
          .Where(bz =>
              _dbContext.FinancialAssets
                  .Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
                  .Select(a => a.ticker)
                  .Contains(bz.ticker)
              && now >= bz.start_date && now <= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync();


      if (betZonesToCheck.Count == 0)
      {
        _logger.Log.Warning("[UpdaterService] :: CheckBets() :: No bets to update!");
        return;
      }

      var betsToUpdate = await _dbContext.Bets
          .Where(b => betZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToListAsync();

      foreach (var currentBet in betsToUpdate)
      {
        var currentBetZone = await _dbContext.BetZones
            .FirstOrDefaultAsync(bz => bz.id == currentBet.bet_zone);

        if (currentBetZone == null)
        {
          _logger.Log.Error("[UpdaterService] :: CheckBets() :: Bet zone null on bet [{0}]", currentBet.id);
          continue;
        }

        var asset = await _dbContext.FinancialAssets
            .FirstOrDefaultAsync(fa => fa.ticker == currentBet.ticker);

        if (asset == null)
        {
          _logger.Log.Error("[UpdaterService] :: CheckBets() :: Asset null on bet [{0}]", currentBet.ticker);
          continue;
        }

        var candles = await _dbContext.AssetCandles
            .Where(c => c.AssetId == asset.id &&
                        c.Interval == "1h" &&
                        c.DateTime >= currentBetZone.start_date &&
                        c.DateTime < currentBetZone.end_date)
            .ToListAsync();

        if (candles.Count == 0)
        {
          _logger.Log.Warning("[UpdaterService] :: CheckBets() :: No candles for [{0}] zone [{1}]", asset.ticker, currentBetZone.id);
          continue;
        }

        double upperBound = currentBetZone.target_value + (currentBetZone.target_value * currentBetZone.bet_margin / 200);
        double lowerBound = currentBetZone.target_value - (currentBetZone.target_value * currentBetZone.bet_margin / 200);

        bool hasExitedZone = candles.Any(c =>
            (double)c.High > upperBound
            ||
            (double)c.Low < lowerBound);

        currentBet.target_won = !hasExitedZone;
        if (hasExitedZone) currentBet.finished = true;
        _dbContext.Bets.Update(currentBet);
      }

      await _dbContext.SaveChangesAsync();
      _logger.Log.Debug("[UpdaterService] :: CheckBets() ended successfully!");
    }

    public async Task CheckUSDBets(bool marketHours)
    {
      _logger.Log.Debug("[UpdaterService] :: CheckUSDBets() called with market hours mode = {0}", marketHours);

      var now = DateTime.UtcNow;

      var betZonesToCheck = (marketHours) ?
        await _dbContext.BetZonesUSD
          .Where(bz => now >= bz.start_date && now <= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync()
        :
        await _dbContext.BetZonesUSD
          .Where(bz =>
              _dbContext.FinancialAssets
                  .Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
                  .Select(a => a.ticker)
                  .Contains(bz.ticker)
              && now >= bz.start_date && now <= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync();


      if (betZonesToCheck.Count == 0)
      {
        _logger.Log.Warning("[UpdaterService] :: CheckUSDBets() :: No bets to update!");
        return;
      }

      var betsToUpdate = await _dbContext.Bets
          .Where(b => betZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToListAsync();

      foreach (var currentBet in betsToUpdate)
      {
        var currentBetZone = await _dbContext.BetZonesUSD
            .FirstOrDefaultAsync(bz => bz.id == currentBet.bet_zone);

        if (currentBetZone == null)
        {
          _logger.Log.Error("[UpdaterService] :: CheckUSDBets() :: Bet zone null on bet [{0}]", currentBet.id);
          continue;
        }

        var asset = await _dbContext.FinancialAssets
            .FirstOrDefaultAsync(fa => fa.ticker == currentBet.ticker);

        if (asset == null)
        {
          _logger.Log.Error("[UpdaterService] :: CheckUSDBets() :: Asset null on bet [{0}]", currentBet.ticker);
          continue;
        }

        var candles = await _dbContext.AssetCandlesUSD
            .Where(c => c.AssetId == asset.id &&
                        c.Interval == "1h" &&
                        c.DateTime >= currentBetZone.start_date &&
                        c.DateTime < currentBetZone.end_date)
            .ToListAsync();

        if (candles.Count == 0)
        {
          _logger.Log.Warning("[UpdaterService] :: CheckUSDBets() :: No candles for [{0}] zone [{1}]", asset.ticker, currentBetZone.id);
          continue;
        }

        double upperBound = currentBetZone.target_value + (currentBetZone.target_value * currentBetZone.bet_margin / 200);
        double lowerBound = currentBetZone.target_value - (currentBetZone.target_value * currentBetZone.bet_margin / 200);

        bool hasExitedZone = candles.Any(c =>
            (double)c.High > upperBound
            ||
            (double)c.Low < lowerBound);

        currentBet.target_won = !hasExitedZone;
        if (hasExitedZone) currentBet.finished = true;
        _dbContext.Bets.Update(currentBet);
      }

      await _dbContext.SaveChangesAsync();
      _logger.Log.Debug("[UpdaterService] :: CheckUSDBets() ended successfully!");
    }

    public async Task PayBets(bool marketHours)
    {
      _logger.Log.Debug("[UpdaterService] :: PayBets() called with mode market hours = {0}", marketHours);

      var betsToPay = (marketHours) ?
        await _dbContext.Bets
          .Where(b => b.finished && !b.paid && b.target_won)
          .ToListAsync()
        :
        await _dbContext.Bets
          .Where(b => b.finished && !b.paid && b.target_won &&
              _dbContext.FinancialAssets
                  .Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
                  .Select(a => a.ticker)
                  .Contains(b.ticker))
          .ToListAsync();

      foreach (var currentBet in betsToPay)
      {
        var winnerUser = _dbContext.Users.FirstOrDefault(u => u.id == currentBet.user_id);

        if (winnerUser != null)
        {
          winnerUser.points += currentBet.bet_amount * currentBet.origin_odds;
          currentBet.paid = true;

          _dbContext.Bets.Update(currentBet);
          _dbContext.Users.Update(winnerUser);

          string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(winnerUser.country, "youWon");
          string msg = string.Format(youWonMessageTemplate,
              (currentBet.bet_amount * currentBet.origin_odds).ToString("N2"),
              currentBet.ticker);

          _ = _firebaseNotificationService.SendNotificationToUser(
              winnerUser.fcm, "Betrader", msg, new() { { "type", "betting" } });

          _logger.Log.Debug("[UpdaterService] :: PayBets() paid to user {0}", winnerUser.id);
        }
      }

      _dbContext.SaveChanges();
      _logger.Log.Debug("[UpdaterService] :: PayBets() ended succesfully!");
    }

    public async Task RefreshTargetOddsAsync(CancellationToken ct = default)
    {
      var now = DateTime.UtcNow;

      await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

      var zones = await _dbContext.BetZones
        .AsNoTracking()
        .Where(bz => bz.active && now < bz.start_date)
        .Select(bz => new { bz.id, bz.ticker, bz.timeframe, bz.start_date })
        .ToListAsync(ct);

      var groups = zones.GroupBy(z => new { z.ticker, z.timeframe, z.start_date });

      foreach (var g in groups)
      {
        var zoneIds = g.Select(z => z.id).ToList();

        var volumes = await _dbContext.Bets
          .Where(b => zoneIds.Contains(b.bet_zone))
          .GroupBy(b => b.bet_zone)
          .Select(x => new { ZoneId = x.Key, Volume = x.Sum(b => b.bet_amount) })
          .ToListAsync(ct);

        if (volumes.Count == 0) continue;

        var volDict = volumes.ToDictionary(x => x.ZoneId, x => (double)x.Volume);

        double k = 1.0;
        double margin = 0.98;
        double total = volumes.Sum(v => v.Volume + k);

        foreach (var v in volumes)
        {
          double prob = (v.Volume + k) / total;
          double odds = Math.Max(1.1, Math.Round((1.0 / prob) * margin, 2));

          await _dbContext.BetZones
            .Where(bz => bz.id == v.ZoneId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.target_odds, _ => odds), ct);
        }
      }

      await tx.CommitAsync(ct);
      _logger.Log.Debug("[UpdaterService] :: RefreshTargetOdds() completed successfully.");
    }

    public void UpdateTrends(bool marketHours)
    {
      using var transaction = _dbContext.Database.BeginTransaction();
      try
      {
        var query = (marketHours) ?
          _dbContext.FinancialAssets
            .AsNoTracking()
            .Where(a => a.current_eur > 0)
            :
            _dbContext.FinancialAssets
            .AsNoTracking()
            .Where(a => a.current_eur > 0 && (a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex"));

        var assets = query.ToList();

        var trends = new List<Trend>();

        foreach (var asset in assets)
        {
          var lastCandle = _dbContext.AssetCandles
              .AsNoTracking()
              .Where(c => c.AssetId == asset.id && c.Interval == "1h")
              .OrderByDescending(c => c.DateTime)
              .FirstOrDefault();

          if (lastCandle == null)
            continue;

          var lastDay = lastCandle.DateTime.Date;

          AssetCandle? prevCandle;

          if (asset.group == "Cryptos" || asset.group == "Forex")
          {
            prevCandle = _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == asset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Skip(24)
                .FirstOrDefault();
          }
          else
          {
            prevCandle = _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == asset.id && c.Interval == "1h" && c.DateTime.Date < lastDay)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefault();
          }

          double prevClose;
          double dailyGain;

          if (prevCandle != null)
          {
            prevClose = (double)prevCandle.Close;
            dailyGain = prevClose == 0 ? 0 : (((double)asset.current_eur - prevClose) / prevClose) * 100.0;
          }
          else
          {
            prevClose = asset.current_eur * 0.95;
            dailyGain = ((asset.current_eur - prevClose) / prevClose) * 100.0;
          }

          trends.Add(new Trend(
              id: 0,
              daily_gain: dailyGain,
              ticker: asset.ticker!
          ));
        }

        var top5 = trends
            .OrderByDescending(x => Math.Abs(x.daily_gain))
            .Take(5)
            .ToList();

        for (int i = 0; i < top5.Count; i++)
          top5[i].id = i + 1;

        var existing = _dbContext.Trends.ToList();
        _dbContext.Trends.RemoveRange(existing);
        _dbContext.Trends.AddRange(top5);
        _dbContext.SaveChanges();

        transaction.Commit();
        _logger.Log.Debug("[UpdaterService] :: UpdateTrends() synced with Favorites()");
      }
      catch (Exception ex)
      {
        _logger.Log.Error($"[UpdaterService] :: UpdateTrends() error: {ex.Message}\n{ex.StackTrace}");
        transaction.Rollback();
      }
    }

    public async Task CheckAndPayPriceBets(bool marketHoursMode)
    {
      _logger.Log.Information("[UpdaterService] :: CheckAndPayPriceBets() called with market hours mode = {0}", marketHoursMode);
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var priceBetsToPay = (marketHoursMode) ?
          await _dbContext.PriceBets
            .Where(pb => !pb.paid && pb.end_date < DateTime.UtcNow)
            .ToListAsync()
          :
          await _dbContext.PriceBets
            .Where(pb =>
                !pb.paid && pb.end_date < DateTime.UtcNow &&
                _dbContext.FinancialAssets
                    .Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
                    .Select(a => a.ticker)
                    .Contains(pb.ticker))
            .ToListAsync();

        foreach (var priceBet in priceBetsToPay)
        {
          var asset = await _dbContext.FinancialAssets
              .FirstOrDefaultAsync(fa => fa.ticker == priceBet.ticker);

          var user = await _dbContext.Users
              .FirstOrDefaultAsync(u => u.id == priceBet.user_id);

          if (asset != null && user != null)
          {
            var lastCandle = await _dbContext.AssetCandles
                .Where(c => c.AssetId == asset.id && c.DateTime <= priceBet.end_date)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefaultAsync();

            double finalClose = lastCandle != null ? (double)lastCandle.Close : asset.current_eur;

            if (Math.Abs(finalClose - priceBet.price_bet) < 0.0001)
            {
              user.points += PRICE_BET_WIN_PRICE;
              priceBet.paid = true;

              _dbContext.PriceBets.Update(priceBet);
              _dbContext.Users.Update(user);

              string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "youWon");
              string msg = string.Format(youWonMessageTemplate, PRICE_BET_WIN_PRICE.ToString("N2"), priceBet.ticker);

              _ = _firebaseNotificationService.SendNotificationToUser(
                  user.fcm,
                  "Betrader",
                  msg,
                  new() { { "type", "price_bet" } }
              );

              _logger.Log.Debug("[UpdaterService] :: CheckAndPayPriceBets :: Paid exact price bet to user {0}", user.id);
            }
            else
            {
              priceBet.paid = true;
              _dbContext.PriceBets.Update(priceBet);

              _logger.Log.Debug("[UpdaterService] :: CheckAndPayPriceBets :: User {0} lost exact price bet on {1}", user.id, priceBet.ticker);
            }
          }
          else
          {
            _logger.Log.Error("[UpdaterService] :: CheckAndPayPriceBets :: Unexistent user or asset for PriceBet ID {0}", priceBet.id);
          }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.Log.Information("[UpdaterService] :: CheckAndPayPriceBets ended successfully!");
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[UpdaterService] :: CheckAndPayPriceBets error. Rolling back transaction");
        await transaction.RollbackAsync();
      }
    }

    public async Task CheckAndPayUSDPriceBets(bool marketHoursMode)
    {
      _logger.Log.Debug("[UpdaterService] :: CheckAndPayUSDPriceBets() called with market hours mode = {0}", marketHoursMode);
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var priceBetsToPay = (marketHoursMode) ?
          await _dbContext.PriceBetsUSD
            .Where(pb => !pb.paid && pb.end_date < DateTime.UtcNow)
            .ToListAsync()
          :
          await _dbContext.PriceBetsUSD
            .Where(pb =>
                !pb.paid && pb.end_date < DateTime.UtcNow &&
                _dbContext.FinancialAssets
                    .Where(a => a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex")
                    .Select(a => a.ticker)
                    .Contains(pb.ticker))
            .ToListAsync();

        foreach (var priceBet in priceBetsToPay)
        {
          var asset = await _dbContext.FinancialAssets
              .FirstOrDefaultAsync(fa => fa.ticker == priceBet.ticker);

          var user = await _dbContext.Users
              .FirstOrDefaultAsync(u => u.id == priceBet.user_id);

          if (asset != null && user != null)
          {
            var lastCandle = await _dbContext.AssetCandlesUSD
                .Where(c => c.AssetId == asset.id && c.DateTime <= priceBet.end_date)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefaultAsync();

            double finalClose = lastCandle != null ? (double)lastCandle.Close : asset.current_eur;

            if (Math.Abs(finalClose - priceBet.price_bet) < 0.0001)
            {
              user.points += PRICE_BET_WIN_PRICE;
              priceBet.paid = true;

              _dbContext.PriceBetsUSD.Update(priceBet);
              _dbContext.Users.Update(user);

              string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "youWon");
              string msg = string.Format(youWonMessageTemplate, PRICE_BET_WIN_PRICE.ToString("N2"), priceBet.ticker);

              _ = _firebaseNotificationService.SendNotificationToUser(
                  user.fcm,
                  "Betrader",
                  msg,
                  new() { { "type", "price_bet" } }
              );

              _logger.Log.Debug("[UpdaterService] :: CheckAndPayUSDPriceBets :: Paid exact price bet to user {0}", user.id);
            }
            else
            {
              priceBet.paid = true;
              _dbContext.PriceBetsUSD.Update(priceBet);

              _logger.Log.Debug("[UpdaterService] :: CheckAndPayUSDPriceBets :: User {0} lost exact price bet on {1}", user.id, priceBet.ticker);
            }
          }
          else
          {
            _logger.Log.Error("[UpdaterService] :: CheckAndPayUSDPriceBets :: Unexistent user or asset for PriceBet ID {0}", priceBet.id);
          }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.Log.Debug("[UpdaterService] :: CheckAndPayUSDPriceBets ended successfully!");
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[UpdaterService] :: CheckAndPayUSDPriceBets error. Rolling back transaction");
        await transaction.RollbackAsync();
      }
    }

    public static string GetCountryByTicker(string ticker)
    {
      string[] parts = ticker.Split('.');

      if (parts.Length != 2)
      {
        throw new ArgumentException("Invalid ticker format. Must be NAME.MARKET. Received: ", ticker);
      }

      string name = parts[0].ToUpper();
      string market = parts[1].ToUpper();

      if (market == "NASDAQ" || market == "NYSE" || market == "NYSEARCA" || market == "USD")
      {
        return "US";
      }
      else if (market == "INDEX")
      {
        return name switch
        {
          "FTSE" => "GB",
          "N225" => "JP",
          "HSI" => "HK",
          "CAC" => "FR",
          "SSEC" => "CN",
          "SENSEX" => "IN",
          "STOXX50E" => "EU",
          "FTSEMIB" => "IT",
          "N100" => "EU",
          "SPTSX60" => "CA",
          "MDAX" => "DE",
          "OBX" => "NO",
          "BEL20" => "BE",
          "AEX" => "NL",
          "PSI20" => "PT",
          "ISEQ20" => "IE",
          "OMXS30" => "SE",
          "OMXH25" => "FI",
          "SMI" => "CH",
          "ATX" => "AT",
          "GDAXI" => "DE",
          "AS51" => "AU",
          "IBEX" => "ES",
          "SPTSE" => "CA",
          "XAU" => "WORLD",
          "XAG" => "WORLD",
          "OIL" => "WORLD",
          "BTC" => "WORLD",
          "ETH" => "WORLD",
          "XRP" => "WORLD",
          "ADA" => "WORLD",
          "DOT" => "WORLD",
          "LTC" => "WORLD",
          "LINK" => "WORLD",
          "BCH" => "WORLD",
          "XLM" => "WORLD",
          "USDC" => "WORLD",
          "UNI" => "WORLD",
          "SOL" => "WORLD",
          "AVAX" => "WORLD",
          "NATGAS" => "WORLD",
          "HG" => "WORLD",
          _ => "WORLD",
        };
      }
      else
      {
        return "WORLD";
      }
    }

    public async Task<string> GetStockIconUrl(string ticker)
    {
      string url = $"https://cloud.iexapis.com/stable/stock/{ticker}/logo?token={MARKETSTACK_KEY}";

      HttpResponseMessage response = await client.GetAsync(url);
      if (response.IsSuccessStatusCode)
      {
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var data = JObject.Parse(jsonResponse);
        return data["url"]!.ToString();
      }
      else
      {
        throw new Exception("Error fetching icon from Google API.");
      }
    }

    private string ConvertImageToBase64(string imageUrl, HttpClient httpClient)
    {
      try
      {
        HttpResponseMessage response = httpClient.GetAsync(imageUrl).Result;
        if (response.IsSuccessStatusCode)
        {
          byte[] imageBytes = response.Content.ReadAsByteArrayAsync().Result;
          return Convert.ToBase64String(imageBytes);
        }
        else
        {
          _logger.Log.Warning($"[UpdaterService] :: Failed to download image from {imageUrl}");
          return string.Empty;
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error($"[UpdaterService] :: Error converting image to Base64: {ex.Message}");
        return string.Empty;
      }
    }
  }

  public class UpdaterHostedService(IServiceProvider serviceProvider, ICustomLogger customLogger) : IHostedService, IDisposable
  {
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ICustomLogger _customLogger = customLogger;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    //private readonly TimeZoneInfo _nyZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Windows
    private readonly TimeZoneInfo _nyZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); // Linux
    private int _assetsBusy = 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Debug("[UpdaterHostedService] :: Service started");
      _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      _backgroundTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
      return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
      while (!ct.IsCancellationRequested)
      {
        var nyTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _nyZone);
        var open = new TimeSpan(9, 30, 0);
        var close = new TimeSpan(16, 0, 0);
        var marketOpen = nyTime.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday && nyTime.TimeOfDay >= open && nyTime.TimeOfDay <= close;

        try
        {
          await ExecuteUpdateAssets(marketOpen);
          ExecuteUpdateTrends(marketOpen);
          await ExecuteCheckBets(marketOpen);
          await ExecuteCreateBets(marketOpen);
        }
        catch (Exception ex)
        {
          _customLogger.Log.Error(ex, "[UpdaterHostedService] :: Error in background loop");
        }

        await Task.Delay(TimeSpan.FromHours(1), ct);
      }
    }

    private async Task ExecuteCreateBets(bool marketHoursMode)
    {
      try
      {
        using var scope = _serviceProvider.CreateScope();
        var updaterService = scope.ServiceProvider.GetRequiredService<UpdaterService>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing CreateBets with mode {0}", (marketHoursMode ? "Market Hours" : "continue mode"));
        await updaterService.CreateBetZones(marketHoursMode);
        _customLogger.Log.Information("[UpdaterHostedService] :: CreateBets execution completed");
      }
      catch (Exception ex)
      {
        _customLogger.Log.Error(ex, "[UpdaterHostedService] :: Error in ExecuteCreateBets. Message: {Message}", ex.Message);
        throw; // Re-lanzar para que el catch del RunAsync también lo capture
      }
    }

    private async Task ExecuteCheckBets(bool marketHoursMode)
    {
      using var scope = _serviceProvider.CreateScope();
      var updaterService = scope.ServiceProvider.GetRequiredService<UpdaterService>();
      _customLogger.Log.Information("[UpdaterHostedService] :: Executing Check bets service with market hours mode: {0}", marketHoursMode.ToString());
      await updaterService.SetInactiveBetZones();
      await updaterService.CheckBets(marketHoursMode);
      await updaterService.CheckUSDBets(marketHoursMode);
      await updaterService.SetFinishedBets();
      await updaterService.SetFinishedUSDBets();
      await updaterService.PayBets(marketHoursMode);
      await updaterService.CheckAndPayPriceBets(marketHoursMode);
      await updaterService.CheckAndPayUSDPriceBets(marketHoursMode);
    }

    private async Task ExecuteUpdateAssets(bool marketHours)
    {
      if (Interlocked.Exchange(ref _assetsBusy, 1) == 1)
      {
        _customLogger.Log.Warning("[UpdaterHostedService] :: UpdateAssets already executing. Skipping.");
        return;
      }

      try
      {
        using var scope = _serviceProvider.CreateScope();
        var updater = scope.ServiceProvider.GetRequiredService<UpdaterService>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateAssets ({0})", marketHours ? "Market hours" : "Continuous");
        await updater.UpdateAssetsAsync(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), marketHours);
      }
      catch (Exception ex)
      {
        _customLogger.Log.Error(ex, "[UpdaterHostedService] :: Error in ExecuteUpdateAssets");
      }
      finally
      {
        Volatile.Write(ref _assetsBusy, 0);
      }
    }

    private void ExecuteUpdateTrends(bool marketHours)
    {
      using var scope = _serviceProvider.CreateScope();
      var updaterService = scope.ServiceProvider.GetRequiredService<UpdaterService>();
      _customLogger.Log.Information("[UpdaterHostedService] :: Executing TrendUpdater service");
      updaterService.UpdateTrends(marketHours);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[UpdaterHostedService] :: Stopping service");
      _cts?.Cancel();
      return Task.CompletedTask;
    }

    public void Dispose()
    {
      _cts?.Cancel();
      _backgroundTask?.Dispose();
    }
  }
}
