
using System;
using System.Collections;
using SerpApi;
using Newtonsoft.Json.Linq;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace BetsTrading_Service.Services
{
  public class TrendUpdater
  {
    const int MAX_TRENDS_ELEMENTS = 5;
    private const string API_KEY= "d9661ef0baa78e225f4ee66ebfb7474202d1cafa808501d174785b04e30a9964";
    private Hashtable ht = new Hashtable() { { "engine", "google_finance_markets" }, { "trend", "most-active" } };
    private static readonly HttpClient client = new HttpClient();

    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public TrendUpdater(AppDbContext dbContext, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _logger = customLogger;

    }

    public void UpdateTrends()
    {
      using (var transaction = _dbContext.Database.BeginTransaction())
      {
        try
        {
          _logger.Log.Error("[Trend Updater] :: UpdateTrends() called!");
          GoogleSearch search = new GoogleSearch(ht, API_KEY);
          JObject data = search.GetJson();
          var market_trends = data["market_trends"];

          var trends = new List<Trend>();
          var mostActive = data["market_trends"].FirstOrDefault(x => (string)x["title"] == "Most active")?["results"];

          if (mostActive != null)
          {
            int i = 1;
            foreach (var item in mostActive)
            {
              if (i <= MAX_TRENDS_ELEMENTS)
              {
                double dailyGain = ((string)item["price_movement"]["movement"] == "Down" ?
                                        -(double)item["price_movement"]["value"] :
                                         (double)item["price_movement"]["value"]);
                double close = ((string)item["price_movement"]["movement"] == "Down" ?
                               (double)item["extracted_price"] + (double)item["price_movement"]["value"] :
                               (double)item["extracted_price"] - (double)item["price_movement"]["value"]);

                string ticker = (string)item["stock"];
                var currentAsset = _dbContext.FinancialAssets.Where(fa => fa.ticker == ticker.Replace(":", ".")).FirstOrDefault();
                
                // Create new asset
                if (currentAsset == null)
                {
                  
                  FinancialAsset tmpAsset = new FinancialAsset(
                      name: (string)item["name"],
                      group: "Shares",
                      icon: "null",
                      country: GetCountryByTicker(ticker.Replace(":", ".")),
                      ticker: ticker.Replace(":","."),
                      current: (double)item["extracted_price"],
                      close: close
                  );
                  _dbContext.FinancialAssets.Add(tmpAsset);
                  _dbContext.SaveChanges(); 
                }

                // Update existent asset
                else if (currentAsset != null) 
                {
                  currentAsset.current = (double)item["extracted_price"];
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

          transaction.Commit();
        }
        catch (DbUpdateConcurrencyException ex)
        {
          _logger.Log.Error("[Trend Updater] :: UpdateTrends() concurrency error :", ex.ToString());
          transaction.Rollback();
        }
        catch (SerpApiSearchException ex)
        {
          _logger.Log.Error("[Trend Updater] :: UpdateTrends() error :", ex.ToString());
          transaction.Rollback();
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[Trend Updater] :: UpdateTrends() unexpected error :", ex.ToString());
          transaction.Rollback();
        }
      }
    }

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
        return data["url"].ToString(); 
      }
      else
      {
        throw new Exception("Error fetching icon from Google API.");
      }
    }

  }

  public class TrendUpdaterHostedService : IHostedService, IDisposable
  {
    const int UPDATE_SECONDS = 21600;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICustomLogger _customLogger;
    private Timer _timer;

    public TrendUpdaterHostedService(IServiceProvider serviceProvider, ICustomLogger customLogger)
    {
      _serviceProvider = serviceProvider;
      _customLogger = customLogger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[TrendUpdaterHostedService] :: Starting the TrendUpdater hosted service.");
      _timer = new Timer(DoWork!, null, TimeSpan.Zero, TimeSpan.FromSeconds(UPDATE_SECONDS));
      return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
      using (var scope = _serviceProvider.CreateScope())
      {
        var scopedServices = scope.ServiceProvider;
        var trendUpdater = scopedServices.GetRequiredService<TrendUpdater>();
        _customLogger.Log.Information("[TrendUpdaterHostedService] :: Executing TrendUpdater service.");
        trendUpdater.UpdateTrends();
      }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _customLogger.Log.Information("[TrendUpdaterHostedService] :: Stopping the TrendUpdater hosted service.");
      _timer?.Change(Timeout.Infinite, 0);
      return Task.CompletedTask;
    }

    public void Dispose()
    {
      _timer?.Dispose();
    }
  }

}
