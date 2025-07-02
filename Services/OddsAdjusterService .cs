using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Services;
using System.Diagnostics;

public class OddsAdjusterService : BackgroundService
{
  private int REFRESH_TIME_SECONDS = 2;

  private readonly IServiceProvider _serviceProvider;
  private readonly ICustomLogger _logger;
  private bool _isRunning = false;

  public OddsAdjusterService(IServiceProvider serviceProvider, ICustomLogger logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.Log.Information("OddsAdjusterService started.");

    while (!stoppingToken.IsCancellationRequested)
    {
      if (!_isRunning)
      {
        _ = Task.Run(async () =>
        {
          _isRunning = true;
          var stopwatch = Stopwatch.StartNew();

          try
          {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firebaseNotificationService = scope.ServiceProvider.GetRequiredService<FirebaseNotificationService>();
            var updater = new Updater(dbContext, _logger, firebaseNotificationService);

            _logger.Log.Debug("AdjustTargetOdds() execution started.");
            updater.RefreshTargetOdds();
            stopwatch.Stop();
            _logger.Log.Debug($"AdjustTargetOdds() execution finished in {stopwatch.ElapsedMilliseconds} ms.");

            AdjustRefreshTime(stopwatch.ElapsedMilliseconds);
          }
          catch (Exception ex)
          {
            _logger.Log.Error(ex, "Error executing AdjustTargetOdds().");
          }
          finally
          {
            _isRunning = false;
          }
        });
      }

      await Task.Delay(TimeSpan.FromSeconds(REFRESH_TIME_SECONDS), stoppingToken);
    }

    _logger.Log.Information("OddsAdjusterService stopped.");
  }

  private void AdjustRefreshTime(long elapsedMs)
  {
    if (elapsedMs > 1500)
    {
      REFRESH_TIME_SECONDS = Math.Min(REFRESH_TIME_SECONDS + 1, 10);
      _logger.Log.Debug($"Execution heavy: increased refresh to {REFRESH_TIME_SECONDS}s.");
    }
    else if (elapsedMs < 700 && REFRESH_TIME_SECONDS > 2)
    {
      REFRESH_TIME_SECONDS--;
      _logger.Log.Debug($"Execution light: decreased refresh to {REFRESH_TIME_SECONDS}s.");
    }
  }
}
