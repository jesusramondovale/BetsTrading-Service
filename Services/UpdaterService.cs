using System;
using System.Collections;
using SerpApi;
using Newtonsoft.Json.Linq;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using BetsTrading_Service.Locale;
using System.Net.Http;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EFCore.BulkExtensions;
using static BetsTrading_Service.Models.TwelveDataParser;


namespace BetsTrading_Service.Services 
{
  public class UpdaterService
  {
    
    private int currentKeyIndex = 0;

    // Assets & Crypto & Forex
    private static readonly string[] TWELVE_DATA_KEYS = Enumerable.Range(0, 11).Select(i => Environment.GetEnvironmentVariable($"TWELVE_DATA_KEY{i}", EnvironmentVariableTarget.User) ?? "").ToArray() ?? [];
    // Logos only
    private string MARKETSTACK_KEY = Environment.GetEnvironmentVariable("MARKETSTACK_API_KEY", EnvironmentVariableTarget.User) ?? "";

    Random random = new Random();

    private const int PRICE_BET_WIN_PRICE = 50000;
    private readonly FirebaseNotificationService _firebaseNotificationService;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    
    private static readonly HttpClient client = new HttpClient();

    #region Constructor
    public UpdaterService(AppDbContext dbContext, ICustomLogger customLogger, FirebaseNotificationService firebaseNotificationService)
    {
      _firebaseNotificationService = firebaseNotificationService;
      _dbContext = dbContext;
      _logger = customLogger;
      
    }

    #endregion

    #region Update Assets

    private string GetCurrentKey() => TWELVE_DATA_KEYS[currentKeyIndex];
    
    private void NextKey()
    {
      currentKeyIndex = (currentKeyIndex + 1) % TWELVE_DATA_KEYS.Length;
    }

    //UpdateAssets with TwelveData key list
    public async Task UpdateAssetsAsync(IServiceScopeFactory scopeFactory, CancellationToken ct = default)
    {
      _logger.Log.Information("[Updater] :: UpdateAssetsAsync() called!");

      if (TWELVE_DATA_KEYS is null || TWELVE_DATA_KEYS.Length == 0 || TWELVE_DATA_KEYS.All(string.IsNullOrWhiteSpace))
      {
        _logger.Log.Error("[Updater] :: TWELVE DATA KEYS not set in environment variables!");
        return;
      }

      using var scope = scopeFactory.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

      var selectedAssets = await db.FinancialAssets
          .AsNoTracking()
          .Where(fa => fa.group == "Shares" || fa.group == "ETF" || fa.group == "Cryptos" || fa.group == "Forex")
          .ToListAsync(ct);

      if (selectedAssets.Count == 0)
      {
        _logger.Log.Error("[Updater] :: ZERO assets found! CHECK DATABASE");
        return;
      }

      using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

      const string interval = "1h";
      const string outputsize = "1000";
      const string desiredQuote = "EUR"; //TO-DO;

      int keyIndex = 0;
      int callsWithThisKey = 0;

      string CurrentKey() => TWELVE_DATA_KEYS[keyIndex] ?? string.Empty;

      void NextKey()
      {
        keyIndex = (keyIndex + 1) % TWELVE_DATA_KEYS.Length;
        callsWithThisKey = 0;
        _logger.Log.Information("[Updater] :: Switching to next TwelveDataKey (index {Index})", keyIndex);
        if (string.IsNullOrWhiteSpace(CurrentKey()))
          _logger.Log.Error("[Updater] :: Current TwelveDataKey is EMPTY, check environment variables!");
      }

      foreach (var asset in selectedAssets)
      {
        ct.ThrowIfCancellationRequested();

        if (callsWithThisKey == 8)
        {
          var wrapped = (keyIndex + 1) % TWELVE_DATA_KEYS.Length == 0;
          NextKey();
          if (wrapped)
          {
            _logger.Log.Information("[Updater] :: Sleeping 35 seconds to bypass rate limit");
            await Task.Delay(TimeSpan.FromSeconds(35), ct);
          }
        }

        var symbol = (asset.ticker ?? string.Empty).Split('.')[0].Trim();
        if (string.IsNullOrWhiteSpace(symbol))
        {
          _logger.Log.Warning("[Updater] :: Asset {Id} has empty ticker, skipping", asset.id);
          continue;
        }

        string url = asset.group == "Cryptos"
            ? $"https://api.twelvedata.com/time_series?symbol={symbol}/{desiredQuote}&interval={interval}&outputsize={outputsize}&apikey={CurrentKey()}"
            : $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={interval}&outputsize={outputsize}&apikey={CurrentKey()}";

        HttpResponseMessage resp;
        try
        {
          resp = await httpClient.GetAsync(url, ct);
          callsWithThisKey++;
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[Updater] :: HTTP error fetching {Symbol}", symbol);
          callsWithThisKey++;
          continue;
        }

        if (!resp.IsSuccessStatusCode)
        {
          _logger.Log.Error("[Updater] :: TwelveData: Failed for {Symbol}. HTTP {Code}", symbol, resp.StatusCode);
          continue;
        }

        string json;
        try
        {
          json = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[Updater] :: Error reading content for {Symbol}", symbol);
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
          _logger.Log.Error(ex, "[Updater] :: JSON parse error for {Symbol}", symbol);
          continue;
        }

        if (parsed == null || parsed.Status?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
        {
          _logger.Log.Warning("[Updater] :: API status not ok for {Symbol}. Raw: {Raw}", symbol, json);
          continue;
        }

        if (parsed.Values == null || parsed.Values.Count == 0)
        {
          _logger.Log.Warning("[Updater] :: No market data for {Symbol}", symbol);
          continue;
        }

        var exchange = parsed.Meta?.Exchange ?? "Unknown";
        var candles = new List<AssetCandle>(parsed.Values.Count);

        foreach (var v in parsed.Values)
        {
          try
          {
            var dtRaw = DateTime.Parse(v.Datetime!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            var dt = new DateTime(dtRaw.Year, dtRaw.Month, dtRaw.Day, dtRaw.Hour, 0, 0, DateTimeKind.Utc);

            candles.Add(new AssetCandle
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
            _logger.Log.Error(ex, "[Updater] :: Parse error for {Symbol} at {Date}", symbol, v.Datetime);
          }
        }

        if (candles.Count == 0)
        {
          _logger.Log.Warning("[Updater] :: No valid candles for {Symbol}", symbol);
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

          await db.BulkInsertOrUpdateAsync(candles, bulkConfig, cancellationToken: ct);

          var lastClose = candles.OrderByDescending(c => c.DateTime).First().Close;
          await db.FinancialAssets
              .Where(f => f.id == asset.id)
              .ExecuteUpdateAsync(s => s.SetProperty(f => f.current, _ => (double)lastClose), ct);

          await transaction.CommitAsync(ct);

          _logger.Log.Debug("[Updater] :: Saved {Count} candles for {Symbol}", candles.Count, symbol);
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[Updater] :: Error saving candles for {Symbol}", symbol);
        }
      }

      _logger.Log.Information("[Updater] :: UpdateAssetsAsync() completed successfully!");
    }



    #endregion

    #region Create Bets
    /*TODO
     *  Separate bets creation on different timers 
     */
    public async Task CreateBets()
      {
        _logger.Log.Information("[Updater] :: CreateBets() called!");
        using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var financialAssets = await _dbContext.FinancialAssets.ToListAsync();

          foreach (var asset in financialAssets)
          {
            var candles = await _dbContext.AssetCandles
                .Where(c => c.AssetId == asset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Take(30)
                .ToListAsync();

            if (candles.Count < 30)
              continue;

            var closes = candles.Select(c => (double)c.Close).ToList();
            var highs = candles.Select(c => (double)c.High).ToList();
            var lows = candles.Select(c => (double)c.Low).ToList();

            double lastClose = closes.First();
            double avgClose = closes.Average();
            double stdDev = Math.Sqrt(closes.Average(c => Math.Pow(c - avgClose, 2)));
            double maxHigh = highs.Max();
            double minLow = lows.Min();
            double priceRange = maxHigh - minLow;

            double slope = CalculateLinearRegressionSlope(closes);
            string trend = slope > stdDev * 0.02 ? "uptrend" :
                           slope < -stdDev * 0.02 ? "downtrend" : "sideways";

            double zoneHeight = priceRange / 3.0;

            double lowLow = minLow;
            double lowHigh = lowLow + zoneHeight;

            double midLow = lowHigh;
            double midHigh = midLow + zoneHeight;

            double highLow = midHigh;
            double highHigh = maxHigh;

            double targetLow = (lowLow + lowHigh) / 2.0;
            double targetMid = (midLow + midHigh) / 2.0;
            double targetHigh = (highLow + highHigh) / 2.0;

            double marginLow = (lowHigh - lowLow) / targetLow * 100.0;
            double marginMid = (midHigh - midLow) / targetMid * 100.0;
            double marginHigh = (highHigh - highLow) / targetHigh * 100.0;

            double oddsLow = EstimateOdds(targetLow, lastClose, stdDev, trend, "low");
            double oddsMid = EstimateOdds(targetMid, lastClose, stdDev, trend, "mid");
            double oddsHigh = EstimateOdds(targetHigh, lastClose, stdDev, trend, "high");

            var now = DateTime.UtcNow;

            var start1h = now.AddHours(2);
            var end1h = now.AddHours(7);
            var start1h_b = now.AddHours(7);
            var end1h_b = now.AddHours(17);

            var start2h = now.AddHours(2);
            var end2h = now.AddHours(12);
            var start2h_b = now.AddHours(12);
            var end2h_b = now.AddHours(32);

            var start4h = now.AddHours(2);
            var end4h = now.AddHours(22);
            var start4h_b = now.AddHours(22);
            var end4h_b = now.AddHours(62);

            var start24h = now.AddHours(2);
            var end24h = now.AddHours(122);
            var start24h_b = now.AddHours(122);
            var end24h_b = now.AddHours(362);

            var horizons = new Dictionary<int, ((DateTime StartA, DateTime EndA), (DateTime StartB, DateTime EndB))>
            {
                { 1, ((start1h, end1h), (start1h_b, end1h_b)) },
                { 2, ((start2h, end2h), (start2h_b, end2h_b)) },
                { 4, ((start4h, end4h), (start4h_b, end4h_b)) },
                { 24, ((start24h, end24h), (start24h_b, end24h_b)) }
            };

            foreach (var h in horizons)
            {
              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetLow,
                  Math.Round(marginLow, 1),
                  h.Value.Item1.StartA,
                  h.Value.Item1.EndA,
                  RandomizeOdds(oddsLow),
                  h.Key
              ));
              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetMid,
                  Math.Round(marginMid, 1),
                  h.Value.Item1.StartA,
                  h.Value.Item1.EndA,
                  RandomizeOdds(oddsMid),
                  h.Key
              ));
              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetHigh,
                  Math.Round(marginHigh, 1),
                  h.Value.Item1.StartA,
                  h.Value.Item1.EndA,
                  RandomizeOdds(oddsHigh),
                  h.Key
              ));

              //------------------------------------

              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetLow,
                  Math.Round(marginLow, 1),
                  h.Value.Item2.StartB,
                  h.Value.Item2.EndB,
                  RandomizeOdds(oddsLow*2),
                  h.Key
              ));
              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetMid,
                  Math.Round(marginMid, 1),
                  h.Value.Item2.StartB,
                  h.Value.Item2.EndB,
                  RandomizeOdds(oddsMid*2),
                  h.Key
              ));
              _dbContext.BetZones.Add(new BetZone(
                  asset.ticker!,
                  targetHigh,
                  Math.Round(marginHigh, 1),
                  h.Value.Item2.StartB,
                  h.Value.Item2.EndB,
                  RandomizeOdds(oddsHigh*2),
                  h.Key
              ));
            }
          }

          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Information("[Updater] :: CreateBets() completed successfully with exact margins.");
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[Updater] :: CreateBets() error");
          await transaction.RollbackAsync();
        }
      }

      }

    double RandomizeOdds(double baseOdds)
    {
      var factor = 1 + (random.NextDouble() * 0.10 - 0.05);
      return Math.Round(baseOdds * factor, 1);
    }

    private double CalculateLinearRegressionSlope(List<double> data)
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

    private double EstimateOdds(double target, double current, double stdDev, string trend, string zone)
      {
        double distance = Math.Abs(current - target);
        double relativeRisk = distance / stdDev;

        double baseOdds = zone switch
        {
          "mid" => 1.8,
          "low" => 2.0,
          "high" => 2.0,
          _ => 2.0
        };

        if (zone == "low" && trend == "uptrend") baseOdds += 0.4;
        if (zone == "high" && trend == "downtrend") baseOdds += 0.4;
        if (zone == "low" && trend == "downtrend") baseOdds -= 0.2;
        if (zone == "high" && trend == "uptrend") baseOdds -= 0.2;

        return Math.Max(1.01, baseOdds + relativeRisk * 0.3);
      }

    public async Task RemoveOldBets()
    {
      _logger.Log.Information("[Updater] :: RemoveOldBets() called!");

      await using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var oldBetZones = await _dbContext.BetZones.ToListAsync();

        foreach (var currentBz in oldBetZones)
        {
          var betsToRemove = await _dbContext.Bet
              .Where(b => b.bet_zone == currentBz.id)
              .ToListAsync();

          if (betsToRemove.Any())
          {
            _dbContext.Bet.RemoveRange(betsToRemove);
          }
        }

        _dbContext.BetZones.RemoveRange(oldBetZones);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.Log.Debug("[Updater] :: RemoveOldBets() completed successfully!");
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[Updater] :: RemoveOldBets() error");
        await transaction.RollbackAsync();
      }
    }


    #endregion

    #region Update Bets
    public Task SetFinishedBets()
    {
      _logger.Log.Information("[Updater] :: SetFinishedBets() called!");

      var betsZonesToCheck = _dbContext.BetZones
          .Where(bz => DateTime.UtcNow >= bz.end_date.AddDays(1))
          .Select(bz => bz.id)
          .ToList();

      if (betsZonesToCheck.Count == 0)
        return Task.CompletedTask;
      var betsToMark = _dbContext.Bet
          .Where(b => betsZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToList();

      foreach (var currentBet in betsToMark)
      {
        currentBet.finished = true;
        _dbContext.Bet.Update(currentBet);
      }

      _dbContext.SaveChanges();
      _logger.Log.Debug("[Updater] :: SetFinishedBets() ended succesfully!");
      return Task.CompletedTask;
    }

    public Task SetInactiveBets()
    {
      _logger.Log.Information("[Updater] :: SetInactiveBets() called!");

      var betZonesToCheck = _dbContext.BetZones
          .Where(bz => bz.start_date <= DateTime.UtcNow)
          .ToList();

      foreach (var currentBetZone in betZonesToCheck)
      {
        currentBetZone.active = false;
        _dbContext.BetZones.Update(currentBetZone);
      }

      _dbContext.SaveChanges();
      _logger.Log.Debug("[Updater] :: SetInactiveBets() ended succesfully!");
      return Task.CompletedTask;
    }

    public async Task CheckBets()
    {
      _logger.Log.Information("[Updater] :: CheckBets() called!");

      var now = DateTime.UtcNow;

      var betZonesToCheck = await _dbContext.BetZones
          .Where(bz => now >= bz.start_date && now <= bz.end_date)
          .Select(bz => bz.id)
          .ToListAsync();

      if (betZonesToCheck.Count == 0)
      {
        _logger.Log.Warning("[Updater] :: CheckBets() :: No bets to update!");
        return;
      }

      var betsToUpdate = await _dbContext.Bet
          .Where(b => betZonesToCheck.Contains(b.bet_zone) && !b.finished)
          .ToListAsync();

      foreach (var currentBet in betsToUpdate)
      {
        var currentBetZone = await _dbContext.BetZones
            .FirstOrDefaultAsync(bz => bz.id == currentBet.bet_zone);

        if (currentBetZone == null)
        {
          _logger.Log.Error("[Updater] :: CheckBets() :: Bet zone null on bet [{0}]", currentBet.id);
          continue;
        }

        var asset = await _dbContext.FinancialAssets
            .FirstOrDefaultAsync(fa => fa.ticker == currentBet.ticker);

        if (asset == null)
        {
          _logger.Log.Error("[Updater] :: CheckBets() :: Asset null on bet [{0}]", currentBet.ticker);
          continue;
        }

        var candles = await _dbContext.AssetCandles
            .Where(c => c.AssetId == asset.id &&
                        c.Interval == "1h" &&
                        c.DateTime >= currentBetZone.start_date &&
                        c.DateTime <= currentBetZone.end_date)
            .ToListAsync();

        if (candles.Count == 0)
        {
          _logger.Log.Warning("[Updater] :: CheckBets() :: No candles for [{0}] zone [{1}]", asset.ticker, currentBetZone.id);
          continue;
        }

        double upperBound = currentBetZone.target_value + (currentBetZone.target_value * currentBetZone.bet_margin / 200);
        double lowerBound = currentBetZone.target_value - (currentBetZone.target_value * currentBetZone.bet_margin / 200);

        bool hasCrossedZone = candles.Any(c =>
            (double)c.High >= lowerBound &&
            (double)c.Low <= upperBound);

        currentBet.target_won = hasCrossedZone;
        currentBet.finished = true;

        _dbContext.Bet.Update(currentBet);
      }

      await _dbContext.SaveChangesAsync();
      _logger.Log.Debug("[Updater] :: CheckBets() ended successfully!");
    }

    public Task PayBets()
    {
      _logger.Log.Information("[Updater] :: PayBets() called!");

      var betsToPay = _dbContext.Bet
          .Where(b => b.finished && !b.paid && b.target_won)
          .ToList();

      foreach (var currentBet in betsToPay)
      {
        var currentBetZone = _dbContext.BetZones.FirstOrDefault(bz => bz.id == currentBet.bet_zone);
        var winnerUser = _dbContext.Users.FirstOrDefault(u => u.id == currentBet.user_id);

        if (winnerUser != null && currentBetZone != null)
        {
          winnerUser.points += currentBet.bet_amount * currentBetZone.target_odds;
          currentBet.paid = true;

          _dbContext.Bet.Update(currentBet);
          _dbContext.Users.Update(winnerUser);

          string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(winnerUser.country, "youWon");
          string msg = string.Format(youWonMessageTemplate,
              (currentBet.bet_amount * currentBetZone.target_odds).ToString("N2"),
              currentBet.ticker);

          _ = _firebaseNotificationService.SendNotificationToUser(
              winnerUser.fcm, "Betrader", msg, new() { { "type", "betting" } });

          _logger.Log.Debug("[Updater] :: PayBets() paid to user {0}", winnerUser.id);
        }
      }

      _dbContext.SaveChanges();
      _logger.Log.Debug("[Updater] :: PayBets() ended succesfully!");
      return Task.CompletedTask;
    }

    #endregion

    #region Refresh odds
    //Called from OddsAdjusterService.cs:41
    public void RefreshTargetOdds()
      {
        _logger.Log.Debug("[Updater] :: RefreshTargetOdds() called!");

        using (var transaction = _dbContext.Database.BeginTransaction())
        {
          try
          {
          
            var betZoneGroups = _dbContext.BetZones
              .GroupBy(bz => new { bz.ticker })
              .ToList();

            foreach (var group in betZoneGroups)
            {
              var zones = group.ToList();
          
              if (zones.Count < 2)
                continue;
          
              var zoneVolumes = zones.Select(zone => new
              {
                Zone = zone,
                Volume = _dbContext.Bet.Where(b => b.bet_zone == zone.id).Sum(b => b.bet_amount)
              }).ToList();
              double totalVolume = zoneVolumes.Sum(z => z.Volume);
          
              if (totalVolume == 0)
                continue;

              foreach (var z in zoneVolumes)
              {
              
                double oppositeVolume = totalVolume - z.Volume;

                double odds = (z.Volume == 0)
                  ? 2.5
                  : (oppositeVolume / z.Volume) * 0.99;

                z.Zone.target_odds = Math.Max(Math.Round(odds, 2),1.1);
                _dbContext.BetZones.Update(z.Zone);

                _logger.Log.Debug("[Updater] :: RefreshTargetOdds() :: ZONE {0} updated: volume={1}, odd={2}",
                  z.Zone.id, z.Volume, odds);
              }
            }

            _dbContext.SaveChanges();
            transaction.Commit();
            _logger.Log.Debug("[Updater] :: RefreshTargetOdds() completed successfully.");
          }
          catch (Exception ex)
          {
            _logger.Log.Error("[Updater] :: AdjustTargetOdds() error: ", ex.ToString());
            transaction.Rollback();
          }
        }
      }
    #endregion

    #region Update Trends
    public void UpdateTrends()
    {
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {

          var lastCandles = _dbContext.AssetCandles
    .Where(ac => ac.Interval == "1h")
    .AsNoTracking()
    .ToList();

          var dailyCloses = lastCandles
              .GroupBy(c => new { c.AssetId, Day = c.DateTime.Date })
              .Select(g => new
              {
                g.Key.AssetId,
                Day = g.Key.Day,
                CloseDaily = g.OrderByDescending(x => x.DateTime).First().Close
              })
              .OrderByDescending(x => x.Day)
              .ToList();

          var gains = dailyCloses
              .GroupBy(x => x.AssetId)
              .Select(g =>
              {
                var ordered = g.OrderByDescending(x => x.Day).Take(2).ToList();
                if (ordered.Count() < 2) return null;

                var last = ordered[0];
                var prev = ordered[1];

                return new
                {
                  AssetId = g.Key,
                  LastDay = last.Day,
                  Gain = (double)((last.CloseDaily - prev.CloseDaily) / prev.CloseDaily * 100m)
                };
              })
              .Where(x => x != null)
              .ToList();

          var top5 = gains
              .Join(_dbContext.FinancialAssets.AsNoTracking(),
                    g => g!.AssetId,
                    fa => fa.id,
                    (g, fa) => new
                    {
                      fa.id,
                      fa.ticker,
                      fa.name,
                      g!.Gain
                    })
              .OrderByDescending(x => Math.Abs(x.Gain))
              .Take(5)
              .ToList();

          var newTrends = new List<Trend>();
          int i = 0;
          foreach (var x in top5)
          {
            newTrends.Add(new Trend(
                id: ++i,
                daily_gain: x.Gain,
                ticker: x.ticker!
            ));
          }

          var existingTrends = _dbContext.Trends
              .ToList();
            
          _dbContext.Trends.RemoveRange(existingTrends);
          _dbContext.Trends.AddRange(newTrends);
          _dbContext.SaveChanges();
          transaction.Commit();
          

          foreach (User user in _dbContext.Users.Where(u => u.is_active).ToList())
          {
            _ = _firebaseNotificationService.SendNotificationToUser(
                user.fcm,
                "Betrader",
                LocalizedTexts.GetTranslationByCountry(user.country, "updatedTrends"),
                new() { { "type", "trends" } }
            );
          }

          _logger.Log.Information("[Updater] :: UpdateTrends() ended successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error($"[Updater] :: UpdateTrends() error: {ex.Message}\n{ex.StackTrace}");
          transaction.Rollback();
        }
      }
    }

    #endregion

    #region Check Price-Bets
    public async Task CheckAndPayPriceBets()
      {
        _logger.Log.Information("[Updater] :: CheckAndPayPriceBets() called!");
        using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var priceBetsToPay = await _dbContext.PriceBets
              .Where(pb => !pb.paid && pb.end_date < DateTime.UtcNow)
              .ToListAsync();

          foreach (var priceBet in priceBetsToPay)
          {
            var asset = await _dbContext.FinancialAssets
                .FirstOrDefaultAsync(fa => fa.ticker == priceBet.ticker);

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.id == priceBet.user_id);

            if (asset != null && user != null)
            {
              // 🔹 Precio real al final del periodo (última vela antes del end_date)
              var lastCandle = await _dbContext.AssetCandles
                  .Where(c => c.AssetId == asset.id && c.DateTime <= priceBet.end_date)
                  .OrderByDescending(c => c.DateTime)
                  .FirstOrDefaultAsync();

              double finalClose = lastCandle != null ? (double)lastCandle.Close : asset.current;

              // WON BET
              if (Math.Abs(finalClose - priceBet.price_bet) < 0.0001) // tolerancia por decimales
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

                _logger.Log.Debug("[Updater] :: CheckAndPayPriceBets :: Paid exact price bet to user {0}", user.id);
              }
              else // LOST BET
              {
                priceBet.paid = true;
                _dbContext.PriceBets.Update(priceBet);

                _logger.Log.Debug("[Updater] :: CheckAndPayPriceBets :: User {0} lost exact price bet on {1}", user.id, priceBet.ticker);
              }
            }
            else
            {
              _logger.Log.Error("[Updater] :: CheckAndPayPriceBets :: Unexistent user or asset for PriceBet ID {0}", priceBet.id);
            }
          }

          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Information("[Updater] :: CheckAndPayPriceBets ended successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error(ex, "[Updater] :: CheckAndPayPriceBets error. Rolling back transaction");
          await transaction.RollbackAsync();
        }
      }

      }

    #endregion

    #region Private methods
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
        switch (name)
        {
          case "FTSE": return "GB"; // UK (Reino Unido)
          case "N225": return "JP"; // Japón
          case "HSI": return "HK";  // Hong Kong
          case "CAC": return "FR";  // Francia
          case "SSEC": return "CN"; // China
          case "SENSEX": return "IN"; // India
          case "STOXX50E": return "EU"; // Unión Europea
          case "FTSEMIB": return "IT"; // Italia
          case "N100": return "EU"; // Unión Europea
          case "SPTSX60": return "CA"; // Canadá
          case "MDAX": return "DE"; // Alemania
          case "OBX": return "NO"; // Noruega
          case "BEL20": return "BE"; // Bélgica
          case "AEX": return "NL"; // Países Bajos
          case "PSI20": return "PT"; // Portugal
          case "ISEQ20": return "IE"; // Irlanda
          case "OMXS30": return "SE"; // Suecia
          case "OMXH25": return "FI"; // Finlandia
          case "SMI": return "CH"; // Suiza
          case "ATX": return "AT"; // Austria
          case "GDAXI": return "DE"; // Alemania
          case "AS51": return "AU"; // Australia
          case "IBEX": return "ES"; // España
          case "SPTSE": return "CA"; // Canadá
          case "XAU": return "WORLD"; // World (para activos globales como oro, plata, etc.)
          case "XAG": return "WORLD"; // World
          case "OIL": return "WORLD"; // World
          case "BTC": return "WORLD"; // World (Bitcoin)
          case "ETH": return "WORLD"; // World (Ethereum)
          case "XRP": return "WORLD"; // World
          case "ADA": return "WORLD"; // World
          case "DOT": return "WORLD"; // World
          case "LTC": return "WORLD"; // World
          case "LINK": return "WORLD"; // World
          case "BCH": return "WORLD"; // World
          case "XLM": return "WORLD"; // World
          case "USDC": return "WORLD"; // World
          case "UNI": return "WORLD"; // World
          case "SOL": return "WORLD"; // World
          case "AVAX": return "WORLD"; // World
          case "NATGAS": return "WORLD"; // World (Gas natural)
          case "HG": return "WORLD"; // World (Cobre)
          default:
            return "WORLD"; // Código por defecto para mercados desconocidos
        }

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
          _logger.Log.Warning($"[Updater] :: Failed to download image from {imageUrl}");
          return string.Empty;
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error($"[Updater] :: Error converting image to Base64: {ex.Message}");
        return string.Empty;
      }
    }
    #endregion
  
  }

  #region UpdaterService
  public class UpdaterHostedService : IHostedService, IDisposable
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly ICustomLogger _customLogger;
    private Timer? _trendsTimer;
    private Timer? _assetsTimer;
    private Timer? _betsTimer;
    private Timer? _createNewBetsTimer;
    private int _assetsBusy = 0;
    


    public UpdaterHostedService(IServiceProvider serviceProvider, ICustomLogger customLogger)

    {
      _serviceProvider = serviceProvider;
      _customLogger = customLogger;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[UpdaterHostedService] :: Starting the Updater hosted service.");

      #if RELEASE
      //TO-DO : config times and more actions
      //_assetsTimer = new Timer(ExecuteUpdateAssets!, null, TimeSpan.FromSeconds(0), TimeSpan.FromHours(1));
      //_trendsTimer = new Timer(ExecuteUpdateTrends!, null, TimeSpan.FromMinutes(3), TimeSpan.FromHours(12));

      //_betsTimer = new Timer(_ =>  { _ = Task.Run(async () =>  { await ExecuteCheckBets(); }); }, null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
      //_createNewBetsTimer = new Timer(_ => { _ = Task.Run(async () => { await ExecuteCleanAndCreateBets(); }); }, null, TimeSpan.FromMinutes(0), TimeSpan.FromHours(3));
      #endif

      return Task.CompletedTask;
    }
    private async Task ExecuteCleanAndCreateBets()
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<UpdaterService>();

        _customLogger.Log.Information("[UpdaterHostedService] :: Executing RemoveOldBets service.");
        await updaterService.RemoveOldBets();

        _customLogger.Log.Information("[UpdaterHostedService] :: Executing CreateBets service.");
        await updaterService.CreateBets();
      }
    }
    private async Task ExecuteCheckBets()
    {
      using var scope = _serviceProvider.CreateScope();
      var updaterService = scope.ServiceProvider.GetRequiredService<UpdaterService>();

      _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateBets service.");

      try
      {
        await updaterService.CheckBets();
        await updaterService.SetInactiveBets();
        await updaterService.SetFinishedBets();
        await updaterService.PayBets();
        await updaterService.CheckAndPayPriceBets();

        _customLogger.Log.Information("[UpdaterHostedService] :: Batch finished successfully!");
      }
      catch (Exception ex)
      {
        _customLogger.Log.Error(ex, "[UpdaterHostedService] :: Error during update batch");
      }
    }
    private async void ExecuteUpdateAssets(object? state)
    {
      if (Interlocked.Exchange(ref _assetsBusy, 1) == 1)
      {
        _customLogger.Log.Warning("[UpdaterHostedService] :: UpdateAssets ya en curso. Skip.");
        return;
      }

      try
      {
        using var scope = _serviceProvider.CreateScope();
        var updater = scope.ServiceProvider.GetRequiredService<UpdaterService>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateAssets service.");
        await updater.UpdateAssetsAsync(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
      }
      catch (Exception ex)
      {
        _customLogger.Log.Error(ex, "[UpdaterHostedService] :: Error en ExecuteUpdateAssets");
      }
      finally
      {
        Volatile.Write(ref _assetsBusy, 0);
      }
    }
    private void ExecuteUpdateTrends(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<UpdaterService>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing TrendUpdater service.");
        updaterService.UpdateTrends();
      }
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[UpdaterHostedService] :: Stopping the TrendUpdater hosted service.");
      _trendsTimer?.Change(Timeout.Infinite, 0);
      return Task.CompletedTask;
    }
    public void Dispose()
    {
      _trendsTimer!.Dispose();
      _assetsTimer!.Dispose();
      _betsTimer!.Dispose();
    }
  }
  #endregion

}
