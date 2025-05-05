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

namespace BetsTrading_Service.Services 
{
  public class Updater
  {

    private string TWELVE_DATA_KEY = Environment.GetEnvironmentVariable("TWELVE_DATA_KEY", EnvironmentVariableTarget.User) ?? "";
    private string MARKETSTACK_KEY = Environment.GetEnvironmentVariable("MARKETSTACK_API_KEY", EnvironmentVariableTarget.User) ?? "";
    private string SERP_API_KEY = Environment.GetEnvironmentVariable("SERP_API_KEY", EnvironmentVariableTarget.User) ?? "";
    private string COINGECKO_API_KEY= Environment.GetEnvironmentVariable("COINGECKO_API_KEY", EnvironmentVariableTarget.User) ?? "";
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

    //TwelveData : Shares
    public void UpdateAssets()
    {
      int i = 0;
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          _logger.Log.Information("[Updater] :: UpdateAssets() called!");

          var selectedAssets = _dbContext.FinancialAssets
              .Where(fa => fa.group.Equals("Shares") || fa.group.Equals("ETF"))
              .ToList();

          if (selectedAssets.Count == 0)
          {
            _logger.Log.Warning("[Updater] :: No assets found for the specified groups (shares).");
            return;
          }

          if (string.IsNullOrEmpty(TWELVE_DATA_KEY))
          {
            _logger.Log.Error("[Updater] :: ALPHA_VANTAGE_KEY not set in user environment variables.");
            return;
          }

          using (HttpClient httpClient = new HttpClient())
          {
            foreach (var asset in selectedAssets)
            {
              // Bypass TwelveData 8 API per min rate limit
              if (i == 7)
              {
                Thread.Sleep(60000);
                i = 0;
              }
              string symbol = asset.ticker?.Split('.')[0] ?? string.Empty;
              if (string.IsNullOrWhiteSpace(symbol))
                continue;
              
              string url = $"https://api.twelvedata.com/time_series?symbol={symbol}&interval=1day&apikey={TWELVE_DATA_KEY}";

              HttpResponseMessage response = httpClient.GetAsync(url).Result;
              if (!response.IsSuccessStatusCode)
              {
                _logger.Log.Error($"[Updater] :: Failed to fetch data for {symbol}. HTTP Status: {response.StatusCode}");
                continue;
              }

              string json = response.Content.ReadAsStringAsync().Result;

              var options = new JsonSerializerOptions
              {
                PropertyNameCaseInsensitive = true
              };

              var parsed = JsonSerializer.Deserialize<CustomAlphaVantageResponse>(json, options);
              if (parsed?.Values == null || !parsed.Values.Any())
              {
                _logger.Log.Warning($"[Updater] :: No market data found for {symbol}");
                i++;
                continue;
              }

              var openPrices = new List<double>();
              var closePrices = new List<double>();
              var maxPrices = new List<double>();
              var minPrices = new List<double>();

              foreach (var day in parsed.Values)
              {
                try
                {
                  double open = double.Parse(day.Open, CultureInfo.InvariantCulture);
                  double high = double.Parse(day.High, CultureInfo.InvariantCulture);
                  double low = double.Parse(day.Low, CultureInfo.InvariantCulture);
                  double close = double.Parse(day.Close, CultureInfo.InvariantCulture);

                  openPrices.Add(open);
                  maxPrices.Add(high);
                  minPrices.Add(low);
                  closePrices.Add(close);
                }
                catch (Exception ex)
                {
                  _logger.Log.Error($"[Updater] :: Error parsing day data for {symbol}: {ex.Message}");
                  i++;
                }
              }

              if (closePrices.Count > 0)
              {
                asset.current = closePrices.First(); // Último cierre disponible
                asset.close = closePrices;
                asset.open = openPrices;
                asset.daily_max = maxPrices;
                asset.daily_min = minPrices;

                _dbContext.FinancialAssets.Update(asset);
              }
              i++;
            }

            _dbContext.SaveChanges();
          }

          transaction.Commit();
          _logger.Log.Information("[Updater] :: UpdateAssets() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error($"[Updater] :: UpdateAssets() error: {ex}");
          transaction.Rollback();
        }
      }
    }

    //TwelveData : Crypto
    public void UpdateCryptos()
    {
      int i = 0;
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          _logger.Log.Information("[Updater] :: UpdateCryptos() called!");

          var selectedAssets = _dbContext.FinancialAssets
              .Where(fa => fa.group.Equals("Cryptos"))
              .ToList();

          if (selectedAssets.Count == 0)
          {
            _logger.Log.Warning("[Updater] :: No assets found for the specified groups (cryptos).");
            return;
          }

          if (string.IsNullOrEmpty(TWELVE_DATA_KEY))
          {
            _logger.Log.Error("[Updater] :: ALPHA_VANTAGE_KEY not set in user environment variables.");
            return;
          }

          using (HttpClient httpClient = new HttpClient())
          {
            foreach (var asset in selectedAssets)
            {
              //Bypass TwelveData 8 API per min rate limit
              if (i == 7)
              {
                Thread.Sleep(60000);
                i = 0;
              }
              string symbol = asset.ticker?.Split('.')[0] ?? string.Empty;
              if (string.IsNullOrWhiteSpace(symbol))
                continue;

              string url = $"https://api.twelvedata.com/time_series?symbol={symbol}/EUR&interval=1day&apikey={TWELVE_DATA_KEY}";

              HttpResponseMessage response = httpClient.GetAsync(url).Result;
              if (!response.IsSuccessStatusCode)
              {
                _logger.Log.Error($"[Updater] :: Failed to fetch data for {symbol}. HTTP Status: {response.StatusCode}");
                continue;
              }

              string json = response.Content.ReadAsStringAsync().Result;

              var options = new JsonSerializerOptions
              {
                PropertyNameCaseInsensitive = true
              };

              var parsed = JsonSerializer.Deserialize<CustomAlphaVantageResponse>(json, options);
              if (parsed?.Values == null || !parsed.Values.Any())
              {
                _logger.Log.Warning($"[Updater] :: No market data found for {symbol}");
                i++;
                continue;
              }

              var openPrices = new List<double>();
              var closePrices = new List<double>();
              var maxPrices = new List<double>();
              var minPrices = new List<double>();

              foreach (var day in parsed.Values)
              {
                try
                {
                  double open = double.Parse(day.Open, CultureInfo.InvariantCulture);
                  double high = double.Parse(day.High, CultureInfo.InvariantCulture);
                  double low = double.Parse(day.Low, CultureInfo.InvariantCulture);
                  double close = double.Parse(day.Close, CultureInfo.InvariantCulture);

                  openPrices.Add(open);
                  maxPrices.Add(high);
                  minPrices.Add(low);
                  closePrices.Add(close);
                }
                catch (Exception ex)
                {
                  _logger.Log.Error($"[Updater] :: Error parsing day data for {symbol}: {ex.Message}");
                  i++;
                }
              }

              if (closePrices.Count > 0)
              {
                asset.current = closePrices.First(); // Último cierre disponible
                asset.close = closePrices;
                asset.open = openPrices;
                asset.daily_max = maxPrices;
                asset.daily_min = minPrices;

                _dbContext.FinancialAssets.Update(asset);
              }
              i++;
            }

            _dbContext.SaveChanges();
          }

          transaction.Commit();
          _logger.Log.Information("[Updater] :: UpdateAssets() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error($"[Updater] :: UpdateAssets() error: {ex}");
          transaction.Rollback();
        }
      }
    }

    //Coingecko : crypto
    public void UpdateTop15Cryptos()
    {
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          _logger.Log.Information("[Updater] :: UpdateCryptos() called!");

          var selectedCryptos = _dbContext.FinancialAssets
              .Where(fa => fa.group.Equals("Cryptos"))
              .ToList();

          using (HttpClient httpClient = new HttpClient())
          {
            
            string apiUrl = "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc";
            
            httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "dotnet");
            httpClient.DefaultRequestHeaders.Add("x-cg-pro-api-key", COINGECKO_API_KEY);

            HttpResponseMessage response = httpClient.GetAsync(apiUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
              _logger.Log.Error($"[Updater] :: Failed to fetch crypto data. HTTP Status: {response.StatusCode}");
              return;
            }

            string responseData = response.Content.ReadAsStringAsync().Result;
            JArray cryptoData = JArray.Parse(responseData);

            if (cryptoData == null || !cryptoData.HasValues)
            {
              _logger.Log.Warning("[Updater] :: No valid crypto data returned.");
              return;
            }
            int maxId = _dbContext.FinancialAssets.Max(fa => fa.id);

            foreach (var crypto in cryptoData)
            {
              try
              {
                string symbol = crypto["symbol"]?.ToString()?.ToUpper() ?? string.Empty;
                string name = crypto["name"]?.ToString() ?? string.Empty;
                string imageUrl = crypto["image"]?.ToString() ?? string.Empty;
                double currentPrice = crypto["current_price"]?.ToObject<double>() ?? 0.0;
                double high24h = crypto["high_24h"]?.ToObject<double>() ?? 0.0;
                double low24h = crypto["low_24h"]?.ToObject<double>() ?? 0.0;
                string lastUpdated = crypto["last_updated"]?.ToString() ?? string.Empty;
                DateTime lastUpdatedDate = DateTime.TryParse(lastUpdated, out DateTime parsedDate) ? parsedDate : DateTime.UtcNow;
                string imageBase64 = ConvertImageToBase64(imageUrl, httpClient);
                var existingCrypto = selectedCryptos.FirstOrDefault(c => c.ticker == symbol);

                if (existingCrypto != null)
                {
                  _logger.Log.Debug($"[Updater] :: UpdateTop15Cryptos :: Crypto {name} ({symbol}) already on DB");
                  continue;
                }
                else
                {
                  
                  _logger.Log.Warning($"[Updater] :: Crypto {name} ({symbol}) is now in the top 15 but is not present in the database. Adding it.");
                  FinancialAsset newCryptoAsset = new FinancialAsset
                  (
                    id: ++maxId,
                    name: name,
                    group: "Cryptos",
                    icon: imageUrl,
                    country: "World",
                    ticker: symbol,
                    current: currentPrice,
                    close: new List<double>{ currentPrice }
                  );
                  
                  _dbContext.FinancialAssets.Add(newCryptoAsset);
                }
              }
              catch (Exception ex)
              {
                _logger.Log.Error($"[Updater] :: Error processing crypto data: {ex.Message}");
              }
            }
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Information("[Updater] :: UpdateCryptos() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error($"[Updater] :: UpdateCryptos() error: {ex}");
          transaction.Rollback();
        }
      }
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
            
            double upperTargetValue = asset.close[0] * 1.2;
            double lowerTargetValue = asset.close[0] * 0.8;
            List <double> closeValue =  asset.close ;
            
            double betMargin = (1.2 - 0.8) * 100; 
            
            if (asset.close.Count > 1)
            {
              BetZone upperBetZone = new BetZone(
                asset.ticker!,               // ticker
                upperTargetValue,           // target_value
                betMargin,                  // bet_margin
                DateTime.Now.AddDays(1),    // start_date
                DateTime.Now.AddDays(4),    // end_date
                (asset.close[0] >= asset.close[1] ? 1.2 : 2.5)  // target_odds 
            );

              BetZone lowerBetZone = new BetZone(
                  asset.ticker!,               // ticker
                  lowerTargetValue,           // target_value
                  betMargin,                  // bet_margin
                  DateTime.Now.AddDays(1),    // start_date
                  DateTime.Now.AddDays(4),    // end_date
                  (asset.close[0] < asset.close[1] ? 1.2 : 2.5)   // target_odds
              );

              _dbContext.BetZones.Add(upperBetZone);
              _dbContext.BetZones.Add(lowerBetZone);
            }
            
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Debug("[Updater] :: CreateBets() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: CreateBets() error: ", ex.ToString());
          transaction.Rollback();
        }
      }
    }

    public void RemoveOldBets()
    {
      _logger.Log.Information("[Updater] :: RemoveOldBets() called!");

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          var oldBetZones = _dbContext.BetZones.Where(bz => bz.start_date < DateTime.Now.AddDays(-15)).ToList();

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

    public void AdjustTargetOdds()
    {
      _logger.Log.Information("[Updater] :: AdjustTargetOdds() called!");

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          var betZones = _dbContext.BetZones.ToList();

          foreach (var betZone in betZones)
          {
            var correspondingBetZone = _dbContext.BetZones
                .FirstOrDefault(bz => bz.ticker == betZone.ticker &&
                                      bz.start_date == betZone.start_date &&
                                      bz.end_date == betZone.end_date &&
                                      bz.id != betZone.id);

            if (correspondingBetZone == null)
            {
              continue;
            }
            
            var betsCurrentZone = _dbContext.Bet.Where(b => b.bet_zone == betZone.id).ToList();
            var betsOppositeZone = _dbContext.Bet.Where(b => b.bet_zone == correspondingBetZone.id).ToList();
            
            double totalBetCurrentZone = betsCurrentZone.Sum(b => b.bet_amount);
            double totalBetOppositeZone = betsOppositeZone.Sum(b => b.bet_amount);
            
            if (totalBetCurrentZone == 0 || totalBetOppositeZone == 0)
            {
              //_logger.Log.Debug("[Updater] :: Zero bets for BetZone with ID {0}", betZone.id);
              continue;
            }
            
            double newOddsCurrentZone = (totalBetOppositeZone / totalBetCurrentZone) * 0.99;
            double newOddsOppositeZone = (totalBetCurrentZone / totalBetOppositeZone) * 0.99;

            
            betZone.target_odds = newOddsCurrentZone;
            _dbContext.BetZones.Update(betZone);

            correspondingBetZone.target_odds = newOddsOppositeZone;
            _dbContext.BetZones.Update(correspondingBetZone);

            _logger.Log.Debug("[Updater] :: Ajustado target_odds en BetZone {0}: CurrentZoneOdds = {1}, OppositeZoneOdds = {2}",
                              betZone.id, newOddsCurrentZone, newOddsOppositeZone);
          }

          _dbContext.SaveChanges();
          transaction.Commit();
          _logger.Log.Debug("[Updater] :: AdjustTargetOdds() completed succesfully.");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: AdjustTargetOdds() error: ", ex.ToString());
          transaction.Rollback();
        }
      }
    }
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
              
              for (int i = startIndex; i <= endIndex && i < financialAsset.daily_max.Count; i++)
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
                  close: new List<double> { (double)item["extracted_price"]! }
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


    public UpdaterHostedService(IServiceProvider serviceProvider, ICustomLogger customLogger)

    {
      _serviceProvider = serviceProvider;
      _customLogger = customLogger;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[UpdaterHostedService] :: Starting the Updater hosted service.");

      #if RELEASE
        _assetsTimer = new Timer(ExecuteUpdateAssets!, null, TimeSpan.FromSeconds(0), TimeSpan.FromHours(6));
        _trendsTimer = new Timer(ExecuteUpdateTrends!, null, TimeSpan.FromSeconds(3615), TimeSpan.FromHours(6));
        _betsTimer = new Timer(ExecuteCheckBets!, null, TimeSpan.FromSeconds(3620), TimeSpan.FromDays(1));
        _createNewBetsTimer = new Timer(ExecuteCleanAndCreateBets!, null, TimeSpan.FromSeconds(3630), TimeSpan.FromDays(3));
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
        updaterService.AdjustTargetOdds();
        updaterService.SetInactiveBets();
        updaterService.SetFinishedBets();
        updaterService.PayBets();
        updaterService.CheckAndPayPriceBets();
      }
    }
    private void ExecuteUpdateAssets(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<Updater>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateAssets service.");
        updaterService.UpdateAssets();
        updaterService.UpdateTop15Cryptos();
        updaterService.UpdateCryptos();
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

  #region JSON Helpers
  public class CustomAlphaVantageResponse
  {
    public CustomMeta Meta { get; set; }
    public List<CustomValue> Values { get; set; }
  }

  public class CustomMeta
  {
    public string Symbol { get; set; }
    public string Interval { get; set; }
    public string Currency { get; set; }
    public string Exchange_Timezone { get; set; }
    public string Exchange { get; set; }
    public string Mic_Code { get; set; }
    public string Type { get; set; }
  }

  public class CustomValue
  {
    public string Datetime { get; set; }
    public string Open { get; set; }
    public string High { get; set; }
    public string Low { get; set; }
    public string Close { get; set; }
    public string Volume { get; set; }
  }


  #endregion

}
