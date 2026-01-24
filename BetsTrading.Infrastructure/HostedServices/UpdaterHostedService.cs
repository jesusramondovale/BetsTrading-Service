using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.HostedServices;

public class UpdaterHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLogger _logger;
    private readonly TimeZoneInfo _nyZone;
    private int _assetsBusy = 0;

    public UpdaterHostedService(
        IServiceProvider serviceProvider,
        IApplicationLogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Try to get timezone, fallback to UTC if not available
        try
        {
            _nyZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch
        {
            try
            {
                _nyZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch
            {
                _nyZone = TimeZoneInfo.Utc;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Debug("[UpdaterHostedService] :: Service started. Waiting 30 seconds before first execution to allow API to be ready...");
        
        // Esperar 30 segundos antes de la primera ejecución para que la API esté lista
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nyTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _nyZone);
                var open = new TimeSpan(9, 30, 0);
                var close = new TimeSpan(16, 0, 0);
                var marketOpen = nyTime.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday 
                    && nyTime.TimeOfDay >= open 
                    && nyTime.TimeOfDay <= close;

                await ExecuteUpdateAssets(marketOpen, stoppingToken);
                await ExecuteUpdateTrends(marketOpen, stoppingToken);
                await ExecuteCheckBets(marketOpen, stoppingToken);
                await ExecuteCreateBets(marketOpen, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[UpdaterHostedService] :: Error in background loop");
            }

            // Wait 1 hour before next iteration
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ExecuteCreateBets(bool marketHoursMode, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var updaterService = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
            _logger.Information("[UpdaterHostedService] :: Executing CreateBets with mode {0}", 
                marketHoursMode ? "Market Hours" : "Continuous");
            await updaterService.CreateBetZonesAsync(marketHoursMode, cancellationToken);
            _logger.Information("[UpdaterHostedService] :: CreateBets execution completed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[UpdaterHostedService] :: Error in ExecuteCreateBets");
        }
    }

    private async Task ExecuteCheckBets(bool marketHoursMode, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var updaterService = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
            _logger.Debug("[UpdaterHostedService] :: Executing Check bets service with market hours mode: {0}", 
                marketHoursMode);
            await updaterService.CheckBetsAsync(marketHoursMode, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[UpdaterHostedService] :: Error in ExecuteCheckBets");
        }
    }

    private async Task ExecuteUpdateAssets(bool marketHours, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _assetsBusy, 1) == 1)
        {
            _logger.Warning("[UpdaterHostedService] :: UpdateAssets already executing. Skipping.");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var updater = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
            _logger.Information("[UpdaterHostedService] :: Executing UpdateAssets ({0})", 
                marketHours ? "Market hours" : "Continuous");
            await updater.UpdateAssetsAsync(marketHours, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[UpdaterHostedService] :: Error in ExecuteUpdateAssets");
        }
        finally
        {
            Volatile.Write(ref _assetsBusy, 0);
        }
    }

    private async Task ExecuteUpdateTrends(bool marketHours, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var updaterService = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
            _logger.Debug("[UpdaterHostedService] :: Executing TrendUpdater service");
            await updaterService.UpdateTrendsAsync(marketHours, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[UpdaterHostedService] :: Error in ExecuteUpdateTrends");
        }
    }
}
