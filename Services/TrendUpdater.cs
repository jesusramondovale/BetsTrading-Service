
using System;
using System.Collections;
using SerpApi;
using Newtonsoft.Json.Linq;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;

namespace BetsTrading_Service.Services
{
  public class TrendUpdater
  {
    private const string API_KEY= "d9661ef0baa78e225f4ee66ebfb7474202d1cafa808501d174785b04e30a9964";
    private Hashtable ht = new Hashtable() { { "engine", "google_finance_markets" }, { "trend", "most-active" } };

    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public TrendUpdater(AppDbContext dbContext, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _logger = customLogger;

    }

    public void UpdateTrends()
    {
      try
      {
        _logger.Log.Error("[Trend Updater] :: UpdateTrends() called! :");
        GoogleSearch search = new GoogleSearch(ht, API_KEY);
        JObject data = search.GetJson();
        var market_trends = data["market_trends"];
        
        //TO-DO:  EXTRACT INFO, PARSE AND WRITE


      }
      catch (SerpApiSearchException ex)
      {
        _logger.Log.Error("[Trend Updater] :: UpdateTrends() error :" , ex.ToString());
      }

    }
  }
}
