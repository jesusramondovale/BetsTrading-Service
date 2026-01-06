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
      _logger.Log.Information("[UpdaterService] :: UpdateAssetsAsync() called! {Mode}", marketHours ? "Mode market hours" : "Continuous mode");

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
        _logger.Log.Information("[UpdaterService] :: Switching to next TwelveDataKey (index {Index})", keyIndex);
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
            _logger.Log.Information("[UpdaterService] :: Sleeping 35 seconds to bypass rate limit");
            await Task.Delay(TimeSpan.FromSeconds(35), ct);
          }
        }

        var symbol = (asset.ticker ?? string.Empty).Split('.')[0].Trim();
        if (string.IsNullOrWhiteSpace(symbol))
        {
          _logger.Log.Warning("[UpdaterService] :: Asset {Id} has empty ticker, skipping", asset.id);
          continue;
        }

        string url = asset.group == "Cryptos"
            ? $"https://api.twelvedata.com/time_series?symbol={symbol}/{desiredQuote}&interval={interval}&timezone=UTC&outputsize={outputsize}&apikey={CurrentKey()}"
            : $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={interval}&timezone=UTC&outputsize={outputsize}&apikey={CurrentKey()}";

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

        var lastDate = await db.AssetCandles
            .Where(c => c.AssetId == asset.id && c.Interval == interval)
            .MaxAsync(c => (DateTime?)c.DateTime, ct) ?? DateTime.MinValue;

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

      _logger.Log.Information("[UpdaterService] :: UpdateAssetsAsync() completed successfully! ({Mode})", marketHours ? "Mode market hours" : "Continuous mode");
    }

    public async Task CreateBetZones(bool marketHoursMode)
    {
      _logger.Log.Information("[UpdaterService] :: CreateBetZones() called with mode market-Hours = {0}", marketHoursMode.ToString());
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var query = !marketHoursMode ?
               _dbContext.FinancialAssets.Where(a => (a.group.ToLower() == "cryptos" || a.group.ToLower() == "forex"))
             : _dbContext.FinancialAssets;

        var financialAssets = await query.ToListAsync();

        foreach (var currentAsset in financialAssets)
        {
          var now = DateTime.UtcNow;

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

          foreach (var timeframeEntry in horizons)
          {
            int timeframe = timeframeEntry.Key;
            var timePeriods = timeframeEntry.Value;
            
            // NO saltar si ya hay zonas activas - queremos generar MÁS zonas para el mismo timeframe
            // Solo verificar que tengamos datos suficientes

            // Obtener más candles para análisis técnico robusto
            var candles = await _dbContext.AssetCandles
                .Where(c => c.AssetId == currentAsset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Take(100) // Más datos para análisis técnico
                .ToListAsync();

            if (candles.Count < 50) continue; // Mínimo 50 candles para análisis confiable

            // Preparar datos
            var closes = candles.Select(c => (double)c.Close).Reverse().ToList(); // Invertir para orden cronológico
            var highs = candles.Select(c => (double)c.High).Reverse().ToList();
            var lows = candles.Select(c => (double)c.Low).Reverse().ToList();
            double currentPrice = closes.Last(); // Precio más reciente

            // Calcular retornos logarítmicos
            var returns = CalculateLogReturns(closes);

            // Calcular volatilidad EWMA (más reactiva que stdDev simple)
            double volatility = CalculateEWMAVolatility(returns);
            if (volatility == 0 || double.IsNaN(volatility))
            {
              // Fallback a desviación estándar si EWMA falla
              double avgClose = closes.Average();
              volatility = Math.Sqrt(closes.Average(c => Math.Pow(c - avgClose, 2))) / avgClose;
            }

            // Calcular drift (tendencia)
            double drift = CalculateDrift(returns);

            // Indicadores técnicos
            double rsi = CalculateRSI(closes);
            var bollinger = CalculateBollingerBands(closes);

            // Detectar soportes y resistencias
            var (supports, resistances) = DetectSupportResistance(highs, lows, closes);

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
            
            for (int periodIndex = 0; periodIndex < maxPeriods; periodIndex++)
            {
              var period = timePeriods[periodIndex];
              double timeToExpiry = (period.End - now).TotalHours;
              
              // Generar exactamente 3 zonas por período (3 filas) para tener 9 zonas totales (3x3)
              int zonesPerPeriod = 3;
              
              // Generar zonas para este período
              var zones = GenerateIntelligentZones(
                currentPrice, supports, resistances, volatility, 
                timeToExpiry, rsi, bollinger, drift, zoneCount: zonesPerPeriod);

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

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.Log.Information("[UpdaterService] :: CreateBetZones() completed successfully with technical analysis. ({0})", 
          (marketHoursMode ? "Mode market hours" : "Continuous mode"));
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[UpdaterService] :: CreateBetZones() error");
        await transaction.RollbackAsync();
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
      (double Upper, double Middle, double Lower) bollinger, double drift, int zoneCount = 6)
    {
      var zones = new List<(double Target, double Margin, double BaseProbability, string ZoneType)>();

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
          2 => new List<double> { -0.05, 0.05 },
          3 => new List<double> { -0.08, 0.08 }, // 3 zonas: una abajo, una actual (ya se añade), una arriba
          _ => new List<double> { -0.08, 0.08 }
        };
      }
      else
      {
        // Para muchas zonas (6+), generar distribución amplia y variada
        zonePercentages.Add(0.0); // Zona actual siempre
        
        // Zonas por debajo: distribuidas desde -12% hasta -2% en incrementos de 1.5%
        for (double pct = -0.12; pct <= -0.02; pct += 0.015)
        {
          zonePercentages.Add(pct);
        }
        
        // Zonas por arriba: distribuidas desde +2% hasta +12% en incrementos de 1.5%
        for (double pct = 0.02; pct <= 0.12; pct += 0.015)
        {
          zonePercentages.Add(pct);
        }
        
        // Para timeframes largos, añadir zonas más extremas
        if (timeToExpiryHours > 12)
        {
          zonePercentages.AddRange(new[] { -0.18, -0.15, -0.20, 0.15, 0.18, 0.20 });
        }
      }

      // 1. ZONA ACTUAL (0% - precio actual) - siempre la primera, probabilidad muy alta
      // Margen mínimo garantizado: al menos 2% del precio para que sea visible
      // Para 3 zonas, usar un margen más pequeño para permitir que las otras zonas se ajusten
      double currentZoneMargin = zoneCount == 3 
        ? Math.Max(currentPrice * 0.015, currentPrice * effectiveVolatility * 0.02) // Margen más pequeño para 3 zonas
        : Math.Max(currentPrice * 0.02, currentPrice * effectiveVolatility * 0.025); // Margen normal para más zonas
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
        if (pct >= -0.20 && pct <= 0.20) // Solo dentro de ±20%
          technicalLevels.Add((support, pct, "support"));
      }

      // Añadir resistencias detectadas
      foreach (var resistance in resistances.Where(r => r > 0 && r > currentPrice * 0.5))
      {
        double pct = (resistance - currentPrice) / currentPrice;
        if (pct >= -0.20 && pct <= 0.20)
          technicalLevels.Add((resistance, pct, "resistance"));
      }

      // Añadir Bollinger Bands
      if (bollinger.Lower > 0)
      {
        double pct = (bollinger.Lower - currentPrice) / currentPrice;
        if (pct >= -0.20 && pct <= 0.20)
          technicalLevels.Add((bollinger.Lower, pct, "bollinger_lower"));
      }
      if (bollinger.Upper > 0)
      {
        double pct = (bollinger.Upper - currentPrice) / currentPrice;
        if (pct >= -0.20 && pct <= 0.20)
          technicalLevels.Add((bollinger.Upper, pct, "bollinger_upper"));
      }

      // Para cada porcentaje objetivo, buscar el nivel técnico más cercano o usar el porcentaje directamente
      foreach (var targetPct in zonePercentages.Where(p => p != 0.0).OrderBy(p => Math.Abs(p)))
      {
        if (zones.Count >= zoneCount) break; // Ya tenemos suficientes zonas

        double targetPrice;
        string zoneType;
        double margin;
        
        // Buscar si hay un nivel técnico cercano a este porcentaje (dentro de 1.5%)
        var closestTechnical = technicalLevels
          .Where(t => Math.Abs(t.Percentage - targetPct) < 0.015)
          .OrderBy(t => Math.Abs(t.Percentage - targetPct))
          .FirstOrDefault();

        if (closestTechnical.Price > 0)
        {
          // Usar el nivel técnico real
          targetPrice = closestTechnical.Price;
          zoneType = closestTechnical.Type;
          // Margen mínimo garantizado: al menos 2% del precio objetivo
          double baseMargin = targetPrice * 0.02;
          double volatilityMargin = targetPrice * effectiveVolatility * 0.03;
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
          double minMarginPercent = zoneCount == 3 ? 0.015 : 0.02; // 1.5% para 3 zonas, 2% para más
          double maxMarginPercent = zoneCount == 3 ? 0.05 : 0.08; // 5% para 3 zonas, 8% para más
          
          // Escalar margen según distancia: desde minMarginPercent (cerca) hasta maxMarginPercent (lejos)
          double marginPercent = minMarginPercent + (distanceFromCurrent / 0.20) * (maxMarginPercent - minMarginPercent);
          marginPercent = Math.Min(maxMarginPercent, Math.Max(minMarginPercent, marginPercent));
          
          margin = targetPrice * marginPercent;
          
          // Asegurar que el margen nunca sea menor que un mínimo absoluto
          double absoluteMinMargin = targetPrice * (zoneCount == 3 ? 0.012 : 0.015); // Mínimo más pequeño para 3 zonas
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
        double prob = CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
        
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
        
        // Garantizar margen mínimo final: nunca menos del 2% del precio objetivo
        double finalMinMargin = targetPrice * 0.02;
        margin = Math.Max(margin, finalMinMargin);
        
        zones.Add((targetPrice, margin, prob, zoneType));
        usedPercentages.Add(targetPct);
      }

      // 3. Si aún no tenemos suficientes zonas, completar con zonas distribuidas uniformemente
      while (zones.Count < zoneCount)
      {
        // Generar zonas en incrementos uniformes
        int missingZones = zoneCount - zones.Count;
        double step = 0.15 / (missingZones + 1); // Distribuir en ±15%
        
        for (int i = 1; i <= missingZones && zones.Count < zoneCount; i++)
        {
          // Alternar entre arriba y abajo
          bool isUp = i % 2 == 0;
          double pct = isUp ? step * i : -step * i;
          
          // Asegurar que no esté muy cerca de zonas existentes
          if (usedPercentages.Any(up => Math.Abs(up - pct) < 0.02))
            continue;
            
          double targetPrice = currentPrice * (1.0 + pct);
          
          // Calcular margen para esta zona antes de verificar solapamiento
          double distanceFromCurrent = Math.Abs(pct);
          double minMarginPercent = 0.02;
          double maxMarginPercent = 0.08;
          double marginPercent = minMarginPercent + (distanceFromCurrent / 0.20) * (maxMarginPercent - minMarginPercent);
          marginPercent = Math.Min(maxMarginPercent, Math.Max(minMarginPercent, marginPercent));
          double tempMargin = targetPrice * marginPercent;
          tempMargin = Math.Max(tempMargin, targetPrice * 0.02);
          
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

          double prob = CalculateReachProbability(currentPrice, targetPrice, effectiveVolatility, timeToExpiryHours, drift);
          prob = Math.Max(0.20, Math.Min(0.60, prob * (1.0 - Math.Abs(pct) * 0.4)));
          
          // Usar el margen ya calculado (tempMargin) que ya verificó no solaparse
          double margin = tempMargin;
          
          zones.Add((targetPrice, margin, prob, isUp ? "above" : "below"));
          usedPercentages.Add(pct);
        }
        
        // Si aún no tenemos suficientes después de varios intentos, usar las zonas que tenemos
        // Asegurar que al menos tengamos la zona actual
        if (zones.Count == 0)
        {
          // Si no hay zonas, crear al menos una zona en el precio actual
          double minMargin = currentPrice * 0.02;
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
    /// Ajusta los márgenes porcentuales de las zonas para que se toquen en sus extremos sin solaparse.
    /// El máximo de una zona coincidirá exactamente con el mínimo de la siguiente.
    /// El margin es porcentual total, y los límites se calculan como:
    /// - upperBound = target + (target * margin / 200)
    /// - lowerBound = target - (target * margin / 200)
    /// </summary>
    private static List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)> AdjustZonesToTouchPercent(
      List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)> zones)
    {
      if (zones.Count <= 1)
        return zones;

      // Ordenar zonas por precio (target) de menor a mayor
      var sortedZones = zones.OrderBy(z => z.Target).ToList();
      var adjustedZones = new List<(double Target, double MarginPercent, double BaseProbability, string ZoneType)>();
      
      // Calcular puntos de contacto entre zonas consecutivas
      // Para que dos zonas se toquen exactamente: upperBound[i] = lowerBound[i+1]
      // target[i] * (1 + margin[i] / 200) = target[i+1] * (1 - margin[i+1] / 200)
      
      // Calcular puntos de contacto como punto medio entre targets adyacentes
      var touchPoints = new List<double>();
      
      for (int i = 0; i < sortedZones.Count - 1; i++)
      {
        var currentZone = sortedZones[i];
        var nextZone = sortedZones[i + 1];
        
        // Calcular el punto medio entre los targets (donde se tocarán exactamente)
        double touchPoint = (currentZone.Target + nextZone.Target) / 2.0;
        touchPoints.Add(touchPoint);
      }
      
      // Ajustar cada zona para que sus límites lleguen exactamente a los puntos de contacto
      for (int i = 0; i < sortedZones.Count; i++)
      {
        var zone = sortedZones[i];
        double adjustedMarginPercent;
        
        if (sortedZones.Count == 1)
        {
          // Si solo hay una zona, mantener el margen original pero con mínimo razonable
          adjustedMarginPercent = Math.Max(2.0, zone.MarginPercent); // Mínimo 2%
        }
        else if (i == 0)
        {
          // Primera zona: el límite superior debe llegar exactamente al primer punto de contacto
          // upperBound = target * (1 + margin / 200) = touchPoint
          // margin / 200 = (touchPoint / target) - 1
          // margin = ((touchPoint / target) - 1) * 200
          double touchPoint = touchPoints[0];
          if (touchPoint > zone.Target)
          {
            adjustedMarginPercent = ((touchPoint / zone.Target) - 1.0) * 200.0;
          }
          else
          {
            // Si el touchPoint está por debajo del target, usar margen mínimo
            adjustedMarginPercent = 2.0;
          }
          adjustedMarginPercent = Math.Max(2.0, adjustedMarginPercent); // Mínimo 2%
        }
        else if (i == sortedZones.Count - 1)
        {
          // Última zona: el límite inferior debe llegar exactamente al último punto de contacto
          // lowerBound = target * (1 - margin / 200) = touchPoint
          // margin / 200 = 1 - (touchPoint / target)
          // margin = (1 - (touchPoint / target)) * 200
          double touchPoint = touchPoints[i - 1];
          if (touchPoint < zone.Target)
          {
            adjustedMarginPercent = (1.0 - (touchPoint / zone.Target)) * 200.0;
          }
          else
          {
            // Si el touchPoint está por encima del target, usar margen mínimo
            adjustedMarginPercent = 2.0;
          }
          adjustedMarginPercent = Math.Max(2.0, adjustedMarginPercent); // Mínimo 2%
        }
        else
        {
          // Zonas intermedias: deben tocar ambos puntos de contacto exactamente
          double lowerTouchPoint = touchPoints[i - 1];
          double upperTouchPoint = touchPoints[i];
          
          // Calcular márgenes necesarios para cada dirección
          // Para el límite inferior:
          double lowerMarginPercent = (1.0 - (lowerTouchPoint / zone.Target)) * 200.0;
          // Para el límite superior:
          double upperMarginPercent = ((upperTouchPoint / zone.Target) - 1.0) * 200.0;
          
          // Usar el máximo para asegurar que toque ambos puntos sin solaparse
          adjustedMarginPercent = Math.Max(lowerMarginPercent, upperMarginPercent);
          adjustedMarginPercent = Math.Max(2.0, adjustedMarginPercent); // Mínimo 2%
        }
        
        adjustedZones.Add((zone.Target, adjustedMarginPercent, zone.BaseProbability, zone.ZoneType));
      }
      
      // Verificar que no haya solapamiento después del ajuste
      for (int i = 0; i < adjustedZones.Count - 1; i++)
      {
        var current = adjustedZones[i];
        var next = adjustedZones[i + 1];
        
        double currentUpper = current.Target * (1.0 + current.MarginPercent / 200.0);
        double nextLower = next.Target * (1.0 - next.MarginPercent / 200.0);
        
        // Si hay solapamiento, ajustar para que se toquen exactamente
        if (currentUpper > nextLower)
        {
          // Calcular el punto medio y ajustar ambas zonas
          double touchPoint = (currentUpper + nextLower) / 2.0;
          
          // Ajustar zona actual
          double newCurrentMargin = ((touchPoint / current.Target) - 1.0) * 200.0;
          adjustedZones[i] = (current.Target, Math.Max(2.0, newCurrentMargin), current.BaseProbability, current.ZoneType);
          
          // Ajustar zona siguiente
          double newNextMargin = (1.0 - (touchPoint / next.Target)) * 200.0;
          adjustedZones[i + 1] = (next.Target, Math.Max(2.0, newNextMargin), next.BaseProbability, next.ZoneType);
        }
      }
      
      return adjustedZones;
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
      _logger.Log.Information("[UpdaterService] :: SetInactiveBetZones() called");

      var now = DateTime.UtcNow;

      var affected = await _dbContext.BetZones
        .Where(bz => bz.active && now >= bz.start_date)
        .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));

      var affectedUSD = await _dbContext.BetZonesUSD
        .Where(bz => bz.active && now >= bz.start_date)
        .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.active, _ => false));


      _logger.Log.Information("[UpdaterService] :: SetInactiveBetZones() -> {Count} deactivated zones", affected);
    }

    public async Task SetFinishedBets()
    {
      _logger.Log.Information("[UpdaterService] :: SetFinishedBets() called");

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
      _logger.Log.Information("[UpdaterService] :: SetFinishedUSDBets() called");

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
      _logger.Log.Information("[UpdaterService] :: CheckBets() called with market hours mode = {0}", marketHours);

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
      _logger.Log.Information("[UpdaterService] :: CheckUSDBets() called with market hours mode = {0}", marketHours);

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
      _logger.Log.Information("[UpdaterService] :: PayBets() called with mode market hours = {0}", marketHours);

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
        _logger.Log.Information("[UpdaterService] :: UpdateTrends() synced with Favorites()");
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
      _logger.Log.Information("[UpdaterService] :: CheckAndPayUSDPriceBets() called with market hours mode = {0}", marketHoursMode);
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
        _logger.Log.Information("[UpdaterService] :: CheckAndPayUSDPriceBets ended successfully!");
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
      _customLogger.Log.Information("[UpdaterHostedService] :: Service started");
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
      using var scope = _serviceProvider.CreateScope();
      var updaterService = scope.ServiceProvider.GetRequiredService<UpdaterService>();
      _customLogger.Log.Information("[UpdaterHostedService] :: Executing CreateBets with mode {0}", (marketHoursMode ? "Market Hours" : "continue mode"));
      await updaterService.CreateBetZones(marketHoursMode);
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
