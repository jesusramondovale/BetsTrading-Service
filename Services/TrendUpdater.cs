
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
                int id = i++;
                string name = (string)item["name"];
                string icon = GetIconBase64(name)!;            
                double dailyGain = (double)item["price_movement"]["percentage"];
                double close = (double)item["extracted_price"];
                double current = close + (double)item["price_movement"]["value"];
                trends.Add(new Trend(id, name, icon, dailyGain, close, current));
              }
              
            }
          }
          
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

    private string? GetIconBase64(string stock)
    {
      var financialAsset = _dbContext.FinancialAssets.FirstOrDefault(fa => fa.name == stock);
      return financialAsset != null ? financialAsset.icon : "null"; 
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
