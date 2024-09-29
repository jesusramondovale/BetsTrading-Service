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

namespace BetsTrading_Service.Services
{
  public class Updater
  {
    const int MAX_TRENDS_ELEMENTS = 5;
    private const string API_KEY= "d9661ef0baa78e225f4ee66ebfb7474202d1cafa808501d174785b04e30a9964";
    private const string API_KEY2 = "d6dc56018991c867fd854be0cc0f2ecf3507d2c45c147516273ed7e91063b248";
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
    //TO-DO: Update assets values 
    public void UpdateAssets()
    {
      //TO-DO
    }
    #endregion

    #region Update Bets
    public void SetFinishedBets()
    {
      _logger.Log.Debug("[Updater] :: SetFinishedBets() called!");
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {

          var betsZonesToCheck = _dbContext.BetZones
             .Where(bz => DateTime.Now >= bz.end_date && bz.end_date >= DateTime.Now.AddDays(-2))
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
      _logger.Log.Debug("[Updater] :: SetInactiveBets() called!");
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
    public void UpdateBets()
    {
      _logger.Log.Debug("[Updater] :: UpdateBets() called!");
      using (var transaction = _dbContext.Database.BeginTransaction())
      {

        try
        {

          var betZonesToCheck = _dbContext.BetZones.Where(bz => DateTime.Now >= bz.start_date && DateTime.Now <= bz.end_date).Select(bz => bz.id).ToList();
          if (0 != betZonesToCheck.Count)
          {
            var betsToUpdate = _dbContext.Bet.Where(b => betZonesToCheck.Contains(b.bet_zone)).ToList();


            foreach (var currentBet in betsToUpdate)
            {

              var currentBetZone = _dbContext.BetZones.FirstOrDefault(bz => bz.id == currentBet.bet_zone);
              if (currentBetZone == null)
              {
                _logger.Log.Error("[Updater] :: UpdateBets() :: Bet zone is null on bet with ID: [{0}]!", currentBet.id);
                continue;
              }

              var financialAsset = _dbContext.FinancialAssets.FirstOrDefault(fa => fa.ticker == currentBet.ticker);
              if (financialAsset == null)
              {
                _logger.Log.Error("[Updater] :: UpdateBets() :: Financial asset is null on bet with Ticker: [{0}]!", currentBet.ticker);
                continue;
              }

              var lowerBound = currentBetZone.target_value - currentBetZone.target_value * currentBetZone.bet_margin / 200;
              var upperBound = currentBetZone.target_value + currentBetZone.target_value * currentBetZone.bet_margin / 200;


              if (financialAsset.current >= lowerBound && financialAsset.current <= upperBound)
              {
                currentBet.target_won = true;
              }
              else
              {
                currentBet.target_won = false;
                currentBet.finished = true;
              }

              _dbContext.Bet.Update(currentBet);
            }

            _dbContext.SaveChanges();
            transaction.Commit();
            _logger.Log.Debug("[Updater] :: UpdateBets() ended succesfrully!");
          }
          else
          {
            _logger.Log.Warning("[Updater] :: UpdateBets() no bets to update!");
          }
          

        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Updater] :: UpdateBets() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Updater] :: UpdateBets() SerpApi error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Updater] :: UpdateBets() unexpected error :", ex.ToString());
          transaction.Rollback();
        }

      }
    }
    public void PayBets()
    {
      _logger.Log.Debug("[Updater] :: PayBets() called!");
      

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
          _logger.Log.Debug("[Updater] :: UpdateTrends() called!");
          GoogleSearch search = new GoogleSearch(ht, API_KEY2);
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
                var currentAsset = _dbContext.FinancialAssets.Where(fa => fa.ticker == ticker.Replace(":", ".")).FirstOrDefault();
                
                // Create new asset
                if (currentAsset == null)
                {
                  
                  FinancialAsset tmpAsset = new FinancialAsset(
                      name: (string)item["name"]!,
                      group: "Shares",
                      icon: "null",
                      country: GetCountryByTicker(ticker.Replace(":", ".")),
                      ticker: ticker.Replace(":","."),
                      current: (double)item["extracted_price"]!,
                      close: close
                  );
                  _dbContext.FinancialAssets.Add(tmpAsset);
                  _dbContext.SaveChanges(); 
                }

                // Update existent asset
                else if (currentAsset != null) 
                {
                  currentAsset.current = (double)item["extracted_price"]!;
                  currentAsset.close = close;
                                    
                  _dbContext.FinancialAssets.Update(currentAsset);
                  _dbContext.SaveChanges();

                }

                trends.Add(new Trend(id: i++, daily_gain: dailyGain, ticker: ticker.Replace(":", ".")));
              }
            }
          }

          // Remover y actualizar los trends existentes
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
    const int SIX_HOURS = 21600; //6h in seconds
    const int ONE_HOUR = 3600; //1h in seconds
    private readonly IServiceProvider _serviceProvider;
    private readonly ICustomLogger _customLogger;
    private Timer? _trendsTimer;
    private Timer? _assetsTimer;
    private Timer? _betsTimer;


    public UpdaterHostedService(IServiceProvider serviceProvider, ICustomLogger customLogger)

    {
      _serviceProvider = serviceProvider;
      _customLogger = customLogger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[UpdaterHostedService] :: Starting the Updater hosted service.");
      
      #if !DEBUG
        _assetsTimer = new Timer(ExecuteUpdateAssets!, null, TimeSpan.Zero, TimeSpan.FromSeconds(ONE_HOUR));
        _trendsTimer = new Timer(ExecuteUpdateTrends!, null, TimeSpan.Zero, TimeSpan.FromSeconds(SIX_HOURS));
        _betsTimer = new Timer(ExecuteUpdateBets!, null, TimeSpan.Zero, TimeSpan.FromSeconds(ONE_HOUR));
      #endif
      
      return Task.CompletedTask;
    }

    private void ExecuteUpdateBets(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var updaterService = scopedServices.GetRequiredService<Updater>();
        _customLogger.Log.Information("[UpdaterHostedService] :: Executing UpdateBets service.");
        updaterService.UpdateBets();
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
