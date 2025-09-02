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
using static BetsTrading_Service.Services.TwelveDataParser;

namespace BetsTrading_Service.Services 
{
  public class Updater
  {
    
    private int currentKeyIndex = 0;

    // Assets & Crypto
    private static readonly string[] TWELVE_DATA_KEYS = Enumerable.Range(0, 10).Select(i => Environment.GetEnvironmentVariable($"TWELVE_DATA_KEY{i}", EnvironmentVariableTarget.User) ?? "").ToArray() ?? [];
    // Logos only
    private string MARKETSTACK_KEY = Environment.GetEnvironmentVariable("MARKETSTACK_API_KEY", EnvironmentVariableTarget.User) ?? "";
    // Trends only
    private string SERP_API_KEY = Environment.GetEnvironmentVariable("SERP_API_KEY", EnvironmentVariableTarget.User) ?? "";

    private const int PRICE_BET_WIN_PRICE = 50000;
    private readonly FirebaseNotificationService _firebaseNotificationService;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    
    private static readonly HttpClient client = new HttpClient();

    #region Constructor
    public Updater(AppDbContext dbContext, ICustomLogger customLogger, FirebaseNotificationService firebaseNotificationService)
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
    public async Task UpdateAssetsAsync(CancellationToken ct = default)
    {
      _logger.Log.Information("[Updater] :: UpdateAssetsAsync() called!");

      if (TWELVE_DATA_KEYS is null || TWELVE_DATA_KEYS.Length == 0 || TWELVE_DATA_KEYS.All(string.IsNullOrWhiteSpace))
      {
        _logger.Log.Error("[Updater] :: TWELVE DATA KEYS not set in user environment variables!");
        return;
      }

      var selectedAssets = await _dbContext.FinancialAssets
          .AsNoTracking()
          .Where(fa => fa.group == "Shares" || fa.group == "ETF" || fa.group == "Cryptos")
          .Select(fa => new { fa.id, fa.ticker, fa.group })
          .ToListAsync(ct);

      if (selectedAssets.Count == 0)
      {
        _logger.Log.Error("[Updater] :: ZERO assets found! CHECK DATABASE");
        return;
      }

      using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

      const string interval = "1day";
      const string outputsize = "90";
      const string desiredQuote = "EUR";

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

        // Rate limit
        if (callsWithThisKey == 8)
        {
          var wrapped = (keyIndex + 1) % TWELVE_DATA_KEYS.Length == 0;
          NextKey();
          if (wrapped)
          {
            _logger.Log.Information("[Updater] :: Sleeping 60 seconds to bypass rate limit");
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
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

        var series = parsed.Values.AsEnumerable();

        var opens = new List<double>(parsed.Values.Count);
        var highs = new List<double>(parsed.Values.Count);
        var lows = new List<double>(parsed.Values.Count);
        var closes = new List<double>(parsed.Values.Count);

        foreach (var day in series)
        {
          try
          {
            var o = double.Parse(day.Open!, CultureInfo.InvariantCulture);
            var h = double.Parse(day.High!, CultureInfo.InvariantCulture);
            var l = double.Parse(day.Low!, CultureInfo.InvariantCulture);
            var c = double.Parse(day.Close!, CultureInfo.InvariantCulture);

            opens.Add(o);
            highs.Add(h);
            lows.Add(l);
            closes.Add(c);
          }
          catch (Exception ex)
          {
            _logger.Log.Error(ex, "[Updater] :: Parse error for {Symbol} item {Date}", symbol, day.Datetime);
          }
        }

        if (closes.Count == 0)
        {
          _logger.Log.Warning("[Updater] :: Empty close series for {Symbol}", symbol);
          continue;
        }

        double current = closes[^1];

        var openArr = opens.ToArray();
        var highArr = highs.ToArray();
        var lowArr = lows.ToArray();
        var closeArr = closes.ToArray();

        const int maxRetries = 3;
        var attempt = 0;

        while (true)
        {
          ct.ThrowIfCancellationRequested();
          try
          {
            var affected = await _dbContext.FinancialAssets
                .Where(f => f.id == asset.id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.current, _ => current)
                    .SetProperty(f => f.open, _ => openArr)
                    .SetProperty(f => f.daily_max, _ => highArr)
                    .SetProperty(f => f.daily_min, _ => lowArr)
                    .SetProperty(f => f.close, _ => closeArr),
                    ct);

            if (affected == 0)
            {
              if (++attempt > maxRetries)
              {
                _logger.Log.Warning("[Updater] :: Zero rows updating {Id} after {Attempts} attempts", asset.id, attempt);
                break;
              }
              await Task.Delay(100 * attempt, ct);
              continue;
            }

            break;
          }
          catch (DbUpdateConcurrencyException ex)
          {
            if (++attempt > maxRetries)
            {
              _logger.Log.Warning(ex, "[Updater] :: Concurrency on {Id} after {Attempts} attempts", asset.id, attempt);
              break;
            }
            await Task.Delay(100 * attempt, ct);
          }
          catch (OperationCanceledException) { throw; }
          catch (Exception ex)
          {
            _logger.Log.Error(ex, "[Updater] :: Error updating {Id}", asset.id);
            break;
          }
        }
      }

      _logger.Log.Information("[Updater] :: UpdateAssetsAsync() completed successfully!");
    }


    #endregion

    #region Create Bets


    public void CreateBets()
    {
      _logger.Log.Information("[Updater] :: CreateBets() called!");

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          var financialAssets = _dbContext.FinancialAssets.ToList();

          foreach (var asset in financialAssets)
          {
            if (asset.close == null || asset.close.Length < 30)
              continue;

            var closes = asset.close.Take(30).ToList();
            var highs = asset!.daily_max!.Take(30).ToList();
            var lows = asset!.daily_min!.Take(30).ToList();

            double lastClose = closes[0];
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

            DateTime start = DateTime.Now.AddDays(1);
            DateTime end = DateTime.Now.AddDays(5);

            _dbContext.BetZones.Add(new BetZone(
              asset.ticker!,
              targetLow,
              Math.Round(marginLow, 2),
              start,
              end,
              Math.Round(oddsLow, 2)
            ));


            _dbContext.BetZones.Add(new BetZone(
              asset.ticker!,
              targetMid,
              Math.Round(marginMid, 2),
              start,
              end,
              Math.Round(oddsMid, 2)
            ));


            _dbContext.BetZones.Add(new BetZone(
              asset.ticker!,
              targetHigh,
              Math.Round(marginHigh, 2),
              start,
              end,
              Math.Round(oddsHigh, 2)
            ));
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Information("[Updater] :: CreateBets() completed successfully with exact margins.");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: CreateBets() error: ", ex.ToString());
          transaction.Rollback();
        }
      }
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


    public void RemoveOldBets()
    {
      _logger.Log.Information("[Updater] :: RemoveOldBets() called!");

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          //var oldBetZones = _dbContext.BetZones.Where(bz => bz.start_date < DateTime.Now.AddDays(-15)).ToList();
          var oldBetZones = _dbContext.BetZones.ToList();

          foreach (var currentBz in oldBetZones)
          {
            var betsToRemove = _dbContext.Bet
                .Where(b => b.bet_zone == currentBz.id)
                .ToList();
          
            if (betsToRemove.Any())
            {
              _dbContext.Bet.RemoveRange(betsToRemove);
            }
          }

          _dbContext.BetZones.RemoveRange(oldBetZones);
          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Debug("[Updater] :: RemoveOldBets() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: RemoveOldBets() error: ", ex.ToString());
          transaction.Rollback();
        }
      }
    }


    #endregion

    #region Update Bets
    public void SetFinishedBets()
    {
      _logger.Log.Information("[Updater] :: SetFinishedBets() called!");
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {

          var betsZonesToCheck = _dbContext.BetZones
             .Where(bz => DateTime.Now >= bz.end_date.AddDays(1))
             .Select(bz => bz.id)
             .ToList();
          
          if (0 != betsZonesToCheck.Count)
          {
            var betsToMark = _dbContext.Bet.Where(b => betsZonesToCheck.Contains(b.bet_zone) && b.finished == false).ToList();

            foreach (var currentBet in betsToMark)
            {
              currentBet.finished = true;
              _dbContext.Bet.Update(currentBet);
            }

            _dbContext.SaveChanges();
            transaction.Commit();
            _logger.Log.Debug("[Updater] :: SetFinishedBets() ended succesfully!");

          }

        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: SetFinishedBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: SetFinishedBets() unexpected error :", ex.ToString());
          transaction.Rollback();
        }
      }
    }
    public void SetInactiveBets()
    {
      _logger.Log.Information("[Updater] :: SetInactiveBets() called!");
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          List <BetZone> betZonesToCheck = _dbContext.BetZones
             .Where(bz => bz.start_date <= DateTime.Now)
             .ToList();
         
          foreach (var currentBetZone in betZonesToCheck)
          {
            currentBetZone.active = false;
            _dbContext.BetZones.Update(currentBetZone);
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Debug("[Updater] :: SetInactiveBets() ended succesfrully!");
          
        }
        
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: SetInactiveBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: SetInactiveBets() unexpected error :", ex.ToString());
          transaction.Rollback();
        }

      }

    }
    public void CheckBets()
    {
      _logger.Log.Information("[Updater] :: CheckBets() called!");
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          var betZonesToCheck = _dbContext.BetZones
              .Where(bz => DateTime.Now >= bz.start_date && DateTime.Now <= bz.end_date)
              .Select(bz => bz.id)
              .ToList();

          if (betZonesToCheck.Count != 0)
          {
            var betsToUpdate = _dbContext.Bet
                .Where(b => betZonesToCheck.Contains(b.bet_zone))
                .ToList();

            foreach (var currentBet in betsToUpdate)
            {
              var currentBetZone = _dbContext.BetZones
                  .FirstOrDefault(bz => bz.id == currentBet.bet_zone && currentBet.finished == false);

              if (currentBetZone == null)
              {
                _logger.Log.Error("[Updater] :: CheckBets() :: Bet zone is null on bet with ID: [{0}]!", currentBet.id);
                continue;
              }

              var financialAsset = _dbContext.FinancialAssets
                  .FirstOrDefault(fa => fa.ticker == currentBet.ticker);

              if (financialAsset == null)
              {
                _logger.Log.Error("[Updater] :: CheckBets() :: Financial asset is null on bet with Ticker: [{0}]!", currentBet.ticker);
                continue;
              }

              if (financialAsset.daily_max == null || financialAsset.daily_min == null)
              {
                _logger.Log.Warning("[Updater] :: CheckBets() :: Daily max/min data is missing for asset with Ticker: [{0}]!", currentBet.ticker);
                continue;
              }
              
              int startIndex = (DateTime.Now - currentBetZone.end_date).Days;
              int endIndex = (DateTime.Now - currentBetZone.start_date).Days;
                            
              startIndex = Math.Max(0, startIndex);
              endIndex = Math.Max(0, endIndex);
                            
              double upperBound = currentBetZone.target_value + (currentBetZone.target_value * currentBetZone.bet_margin / 200);
              double lowerBound = currentBetZone.target_value - (currentBetZone.target_value * currentBetZone.bet_margin / 200);

              bool hasCrossedZone = false;
              
              for (int i = startIndex; i <= endIndex && i < financialAsset.daily_max.Length; i++)
              {
                double dayMax = financialAsset.daily_max[i];
                double dayMin = financialAsset.daily_min[i];

                if (dayMax >= lowerBound && dayMin <= upperBound)
                {
                  hasCrossedZone = true;
                  break;
                }
              }

              currentBet.target_won = hasCrossedZone;
              currentBet.finished = true;

              _dbContext.Bet.Update(currentBet);
            }

            _dbContext.SaveChanges();
            transaction.Commit();
            _logger.Log.Debug("[Updater] :: CheckBets() ended successfully!");
          }
          else
          {
            _logger.Log.Warning("[Updater] :: CheckBets() :: No bets to update!");
          }
        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: CheckBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: CheckBets() unexpected error :", ex.ToString());
          transaction.Rollback();
        }
      }
    }
    public void PayBets()
    {
      _logger.Log.Information("[Updater] :: PayBets() called!");
      
      using (var transaction = _dbContext.Database.BeginTransaction())
      {

        try
        {
          var betsToPay = _dbContext.Bet.Where(b => b.finished == true && b.paid == false && b.target_won == true).ToList();

          foreach (var currentBet in betsToPay)
          {

            var currentBetZone = _dbContext.BetZones.Where(bz => bz.id == currentBet.bet_zone).FirstOrDefault();
            
            
            var winnerUser = _dbContext.Users.Where(u => u.id == currentBet.user_id).FirstOrDefault();

            if (winnerUser != null && currentBetZone != null)
            {
              winnerUser.points += currentBet.bet_amount * currentBetZone.target_odds;
              currentBet.paid = true;

              _dbContext.Bet.Update(currentBet);
              _dbContext.Users.Update(winnerUser);
              
              string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(winnerUser.country, "youWon");
              string msg = string.Format(youWonMessageTemplate, (currentBet.bet_amount * currentBetZone.target_odds).ToString("N2"), currentBet.ticker);

              _ = _firebaseNotificationService.SendNotificationToUser(winnerUser.fcm, "Betrader", msg, new() { { "type", "betting" } });
              _logger.Log.Debug("[Updater] :: PayBets() paid to user {0}", winnerUser.id);
            }

            
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Debug("[Updater] :: PayBets() ended succesfully!");

        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: PayBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: PayBets() SerpApi rror :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: PayBets() unexpected error :", ex.ToString());
          transaction.Rollback();
        }

      }
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
          Hashtable ht = new Hashtable() { { "engine", "google_finance_markets" }, { "trend", "most-active" } };
          _logger.Log.Information("[Updater] :: UpdateTrends() called!");

          GoogleSearch search = new GoogleSearch(ht, SERP_API_KEY);
          JObject data = search.GetJson();
          var mostActive = data["market_trends"]!.FirstOrDefault(x => (string)x["title"]! == "Most active")?["results"];

          if (mostActive == null || mostActive.Count() == 0)
          {
            _logger.Log.Warning("[Updater] :: No active trends found.");
            return;
          }
          
          var existingTrends = _dbContext.Trends.ToList();
          _dbContext.Trends.RemoveRange(existingTrends);
          _dbContext.SaveChanges();
          
          var top5Trending = mostActive
              .OrderByDescending(x => (double)x["price_movement"]!["percentage"]!)
              .Take(5)
              .ToList();

          var newTrends = new List<Trend>();
          int maxId = _dbContext.FinancialAssets.Max(fa => fa.id);

          for (int i = 0; i < top5Trending.Count; i++)
          {
            var item = top5Trending[i];
            string ticker = ((string)item["stock"]!).Replace(":", ".");

            double dailyGain = ((string)item["price_movement"]!["movement"]! == "Down" ?
                               -(double)item["price_movement"]!["value"]! :
                                (double)item["price_movement"]!["value"]!);

            newTrends.Add(new Trend(id: i + 1, daily_gain: dailyGain, ticker: ticker));
            var currentAsset = _dbContext.FinancialAssets.AsNoTracking().Where(fa => fa.ticker == ticker.Replace(":", ".")).FirstOrDefault();

            if (currentAsset == null)
            {

              FinancialAsset tmpAsset = new FinancialAsset(
                  id: ++maxId,
                  name: (string)item["name"]!,
                  group: "Shares",
                  icon: "null",
                  country: GetCountryByTicker(ticker.Replace(":", ".")),
                  ticker: ticker.Replace(":", "."),
                  current: (double)item["extracted_price"]!,
                  close: new double[] { (double)item["extracted_price"]! }
              );

              _dbContext.FinancialAssets.Add(tmpAsset);
              _dbContext.SaveChanges();
            }


          }

          _dbContext.Trends.AddRange(newTrends);
          _dbContext.SaveChanges();

          
          foreach (User user in _dbContext.Users.ToList())
          {
            _ = _firebaseNotificationService.SendNotificationToUser(
                user.fcm, "Betrader",
                LocalizedTexts.GetTranslationByCountry(user.country, "updatedTrends"),
                new() { { "type", "trends" } }
            );
          }

          transaction.Commit();
          _logger.Log.Debug("[Updater] :: UpdateTrends() ended successfully!");
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
    public void CheckAndPayPriceBets()
    {
      _logger.Log.Information("[Updater] :: CheckAndPayPriceBets() called!");

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          var priceBetsToPay = _dbContext.PriceBets.Where(pb => !pb.paid && pb.end_date < DateTime.UtcNow).ToList();

          foreach (var priceBet in priceBetsToPay)
          {
            var asset = _dbContext.FinancialAssets.FirstOrDefault(fa => fa.ticker == priceBet.ticker);

            var user = _dbContext.Users.FirstOrDefault(u => u.id == priceBet.user_id);

            if (asset != null && user != null)
            {
              
              //TODO
              //WON BET
              if (asset.close!.Last() == priceBet.price_bet)
              {
                user.points += PRICE_BET_WIN_PRICE;
                priceBet.paid = true;
                _dbContext.PriceBets.Update(priceBet);
                _dbContext.Users.Update(user);

                string youWonMessageTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "youWon");
                string msg = string.Format(youWonMessageTemplate, (PRICE_BET_WIN_PRICE).ToString("N2"), priceBet.ticker);

                _ = _firebaseNotificationService.SendNotificationToUser(user.fcm, "Betrader", msg, new() { { "type", "price_bet" } });
                _logger.Log.Debug("[Updater] :: CheckAndPayPriceBets :: Paid exact price bet to user {0}", user.id);
              }
              //LOST BET
              else
              {
                priceBet.paid = true;
                _dbContext.PriceBets.Update(priceBet);
                _logger.Log.Debug("[Updater] :: CheckAndPayPriceBets :: User {0} lost exact price bet on {1}", user.id, priceBet.ticker);
              }
            }
            // UNEXISTENT USER
            else
            {
              _logger.Log.Error("[Updater] :: CheckAndPayPriceBets ::  Unexistent user or asset!");
            }
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Information("[Updater] :: CheckAndPayPriceBets ended successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: CheckAndPayPriceBets error: {0}. Rolling back transaction", ex.ToString());
          transaction.Rollback();
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
        _assetsTimer = new Timer(ExecuteUpdateAssets!, null, TimeSpan.FromSeconds(0), TimeSpan.FromDays(1));
        _trendsTimer = new Timer(ExecuteUpdateTrends!, null, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1));
        _betsTimer = new Timer(ExecuteCheckBets!, null, TimeSpan.FromMinutes(7), TimeSpan.FromDays(1));
        _createNewBetsTimer = new Timer(ExecuteCleanAndCreateBets!, null, TimeSpan.FromMinutes(10), TimeSpan.FromDays(4));
      #endif

      return Task.CompletedTask;
    }
    private void ExecuteCleanAndCreateBets(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<Updater>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing RemoveOldBets service.");
        updaterService.RemoveOldBets();

        _customLogger.Log.Information("[UpdaterHostedService] :: Executing CreateBets service.");
        updaterService.CreateBets();
      }
    }
    private void ExecuteCheckBets(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<Updater>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateBets service.");
        updaterService.CheckBets();
        updaterService.SetInactiveBets();
        updaterService.SetFinishedBets();
        updaterService.PayBets();
        updaterService.CheckAndPayPriceBets();
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
        var updater = scope.ServiceProvider.GetRequiredService<Updater>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateAssets service.");
        await updater.UpdateAssetsAsync();
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
        var updaterService = scopedServices.GetRequiredService<Updater>();
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

  #region TwelveData API parser
  public class TwelveDataParser
  {
    public CustomMeta? Meta { get; set; }
    public List<CustomValue>? Values { get; set; }

    public class CustomMeta
    {
      public string? Symbol { get; set; }
      public string? Interval { get; set; }
      public string? Currency { get; set; }
      public string? Exchange_Timezone { get; set; }
      public string? Exchange { get; set; }
      public string? Mic_Code { get; set; }
      public string? Type { get; set; }
    }

    public class CustomValue
    {
      public string? Datetime { get; set; }
      public string? Open { get; set; }
      public string? High { get; set; }
      public string? Low { get; set; }
      public string? Close { get; set; }
      public string? Volume { get; set; }
    }

    public sealed class TwelveDataResponse
    {
      public TwelveMeta? Meta { get; set; }
      public List<TwelveBar> Values { get; set; } = new();
      public string? Status { get; set; }  // "ok" o "error"
                                           // Si hay error, podrían venir campos "code" y "message"
      public object? Code { get; set; }
      public object? Message { get; set; }
    }

    public sealed class TwelveMeta
    {
      public string? Symbol { get; set; }          // p.ej., "BTC/EUR"
      public string? Interval { get; set; }        // "1day"
      public string? Currency_Base { get; set; }   // "Bitcoin"
      public string? Currency_Quote { get; set; }  // "Euro"
      public string? Exchange { get; set; }        // "Coinbase Pro"
      public string? Type { get; set; }            // "Digital Currency"
    }

    public sealed class TwelveBar
    {
      public string? Datetime { get; set; } // "2025-09-02"
      public string? Open { get; set; }
      public string? High { get; set; }
      public string? Low { get; set; }
      public string? Close { get; set; }
    }


  }
  #endregion

}
