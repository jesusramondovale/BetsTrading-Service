
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
        //var market_trends = data["market_trends"];
        
        //TO-DO:  EXTRACT INFO, PARSE AND WRITE


      }
      catch (SerpApiSearchException ex)
      {
        _logger.Log.Error("[Trend Updater] :: UpdateTrends() error :" , ex.ToString());
      }

    }
  }

  public class TrendUpdaterHostedService : IHostedService, IDisposable
  {
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
      _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(6));
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
