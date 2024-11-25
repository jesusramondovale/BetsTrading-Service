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

namespace BetsTrading_Service.Services
{
  public class Updater
  {
    const int MAX_TRENDS_ELEMENTS = 5;
    private const string API_KEY= "d9661ef0baa78e225f4ee66ebfb7474202d1cafa808501d174785b04e30a9964";
    private const string API_KEY2 = "d6dc56018991c867fd854be0cc0f2ecf3507d2c45c147516273ed7e91063b248";

    private const string ALPHA_KEY1 = "O5NQS4V0GAZ4A643";
    private const string ALPHA_KEY2 = "VRD5VVW67S44FW47";
    private const string ALPHA_KEY3 = "3R6SY725SPIGNPHD";
    private const string ALPHA_KEY4 = "12K5J6WV68K2XQ06";
    private const string ALPHA_KEY5 = "8F9UJZ23JB4MQ6G9";
    private const string ALPHA_KEY6 = "JHG6XNRKWBLRCWX4";
    private const string ALPHA_KEY7 = "3G5ARW1VUDF629BM";

    private static readonly string[] ALPHA_KEYS = {
      ALPHA_KEY1,
      ALPHA_KEY2,
      ALPHA_KEY3,
      ALPHA_KEY4,
      ALPHA_KEY5,
      ALPHA_KEY6,
      ALPHA_KEY7
    };


    private Hashtable ht = new Hashtable() { { "engine", "google_finance_markets" }, { "trend", "most-active" } };
    private static readonly HttpClient client = new HttpClient();

    private readonly FirebaseNotificationService _firebaseNotificationService;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public Updater(AppDbContext dbContext, ICustomLogger customLogger, FirebaseNotificationService firebaseNotificationService)
    {
      _firebaseNotificationService = firebaseNotificationService;
      _dbContext = dbContext;
      _logger = customLogger;

    }

    #region Update Assets

    public void UpdateAssets()
    {
      string ALPHA_KEY = ALPHA_KEYS[new Random().Next(ALPHA_KEYS.Length)];

      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          _logger.Log.Information("[Updater] :: UpdateAssets() called!");


          var selectedAssets = _dbContext.FinancialAssets.Where(fa => fa.group.Equals("Cryptos") 
                                                             || fa.group.Equals("Shares")
                                                             || fa.group.Equals("Commodities"))
                                                              .ToList();
          
          if (selectedAssets.Count == 0)
          {
            _logger.Log.Warning("[Updater] :: UpdateAssets() :: No assets found for the specified IDs.");
            return;
          }
          
          using (HttpClient httpClient = new HttpClient())
          {
            foreach (var asset in selectedAssets)
            {
              string symbol = string.Empty;
              if (asset.group.Equals("Cryptos")){
                symbol = $"{asset.ticker!.Split('.')[0]}{asset.ticker!.Split('.')[1]}";
              }
              else
              {
                symbol = asset.ticker!.Split('.')[0];
              }
              string apiUrl = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={ALPHA_KEY}";
              HttpResponseMessage response = httpClient.GetAsync(apiUrl).Result;

              if (response.IsSuccessStatusCode)
              {
                string responseData = response.Content.ReadAsStringAsync().Result;
                JObject jsonData = JObject.Parse(responseData);

                var timeSeries = jsonData["Time Series (Daily)"];
                if (timeSeries != null)
                {
                  var closePrices = new List<double>();
                  var openPrices = new List<double>();
                  var maxPrices = new List<double>();
                  var minPrices = new List<double>();


                  if (timeSeries is JObject timeSeriesObject)
                  {
                    foreach (var dayEntry in timeSeriesObject)
                    {
                      string date = dayEntry.Key; 
                      var dayData = dayEntry.Value;

                      double open = (double)dayData!["1. open"]!;
                      double high = (double)dayData["2. high"]!;
                      double low = (double)dayData["3. low"]!;
                      double close = (double)dayData["4. close"]!;

                      openPrices.Add(open);
                      maxPrices.Add(high);
                      minPrices.Add(low);
                      closePrices.Add(close);
                    }
                  }
                  else
                  {
                    _logger.Log.Warning("[Updater] :: Time Series data is not in the expected format.");
                  }

                  
                  asset.current = closePrices.First(); 
                  asset.close = closePrices;          
                  asset.open = openPrices;           
                  asset.daily_max = maxPrices;       
                  asset.daily_min = minPrices;      

                  
                  _dbContext.FinancialAssets.Update(asset);
                  _dbContext.SaveChanges();
                }
                else
                {
                  _logger.Log.Warning($"[Updater] :: No time series data found for ticker: {asset.ticker}");
                }
              }
              else
              {
                _logger.Log.Error($"[Updater] :: Failed to fetch data for ticker: {asset.ticker}. HTTP Status: {response.StatusCode}");
              }
            }
          }

          transaction.Commit();
          _logger.Log.Information("[Updater] :: UpdateAssets() completed successfully!");
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: UpdateAssets() error: ", ex.ToString());
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
              _logger.Log.Warning("[Updater] :: Zero bets for BetZone with ID {0}", betZone.id);
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
            _logger.Log.Debug("[Updater] :: SetFinishedBets() ended succesfrully!");

          }

        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: SetFinishedBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: SetFinishedBets() SerpApi error :", ex.ToString());
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
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: SetInactiveBets() SerpApi error :", ex.ToString());
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
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: CheckBets() SerpApi error :", ex.ToString());
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

              _ = _firebaseNotificationService.SendNotificationToUser(winnerUser.fcm, "Betrader", msg);
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
          _logger.Log.Information("[Updater] :: UpdateTrends() called!");
          GoogleSearch search = new GoogleSearch(ht, API_KEY);
          JObject data = search.GetJson();
          var market_trends = data["market_trends"];

          var trends = new List<Trend>();
          var mostActive = data["market_trends"]!.FirstOrDefault(x => (string)x["title"]! == "Most active")?["results"];

          if (null != mostActive && mostActive.Count() != 0)
          {
            int i = 1;
            foreach (var item in mostActive)
            {
              if (i <= MAX_TRENDS_ELEMENTS)
              {
                double dailyGain = ((string)item["price_movement"]!["movement"]! == "Down" ?
                                        -(double)item["price_movement"]!["value"]! :
                                         (double)item["price_movement"]!["value"]!);
                double close = ((string)item["price_movement"]!["movement"]! == "Down" ?
                               (double)item["extracted_price"]! + (double)item["price_movement"]!["value"]! :
                               (double)item["extracted_price"]! - (double)item["price_movement"]!["value"]!);

                
                string ticker = (string)item!["stock"]!;
                                
                trends.Add(new Trend(id: i++, daily_gain: dailyGain, ticker: ticker.Replace(":", ".")));

                var currentAsset = _dbContext.FinancialAssets.AsNoTracking().Where(fa => fa.ticker == ticker.Replace(":", ".")).FirstOrDefault();

                int maxId = _dbContext.FinancialAssets.Max(fa => fa.id);
                
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
            }
          }
          
          var existingTrends = _dbContext.Trends.ToList();
          _dbContext.Trends.RemoveRange(existingTrends);
          _dbContext.SaveChanges();

          _dbContext.Trends.AddRange(trends);
          _dbContext.SaveChanges();
          
          foreach (User user in _dbContext.Users.ToList())
          {
            
            _ = _firebaseNotificationService.SendNotificationToUser(user.fcm, "Betrader", LocalizedTexts.GetTranslationByCountry(user.country,"updatedTrends"));
          }

          transaction.Commit();
          _logger.Log.Debug("[Updater] :: UpdateTrends() ended succesfrully!");
        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: UpdateTrends() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: UpdateTrends() SerpApi error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: UpdateTrends() unexpected error :", ex.ToString());
          transaction.Rollback();
        }
      }
    }
    
    #endregion

    #region Private methods
    public static string GetCountryByTicker(string ticker)
    {
      // Separar el ticker en dos partes: nombre del activo y mercado
      string[] parts = ticker.Split('.');

      if (parts.Length != 2)
      {
        throw new ArgumentException("Invalid ticker format. Must be NAME.MARKET. Received: ", ticker);
      }

      string name = parts[0].ToUpper(); 
      string market = parts[1].ToUpper(); 

      // Comprobar si el mercado es de EE.UU.
      if (market == "NASDAQ" || market == "NYSE" || market == "NYSEARCA" || market == "USD")
      {
        return "US"; // Código internacional de USA
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

    public static async Task<string> GetStockIconUrl(string ticker)
    {
      string url = $"https://cloud.iexapis.com/stable/stock/{ticker}/logo?token={API_KEY}";

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
    #endregion
  }

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
        _assetsTimer = new Timer(ExecuteUpdateAssets!, null, TimeSpan.FromSeconds(0), TimeSpan.FromDays(1));
        _trendsTimer = new Timer(ExecuteUpdateTrends!, null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(6));
        _betsTimer = new Timer(ExecuteCheckBets!, null, TimeSpan.FromSeconds(60), TimeSpan.FromDays(1));
        _createNewBetsTimer = new Timer(ExecuteCleanAndCreateBets!, null, TimeSpan.FromSeconds(90), TimeSpan.FromDays(3));
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

}
