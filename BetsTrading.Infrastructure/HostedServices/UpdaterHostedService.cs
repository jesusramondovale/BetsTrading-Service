using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.HostedServices;

public class UpdaterHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLogger _logger;
    private readonly IAdminRuntimeConfig _adminConfig;
    private readonly TimeZoneInfo _nyZone;
    private int _assetsBusy = 0;

    public UpdaterHostedService(
        IServiceProvider serviceProvider,
        IApplicationLogger logger,
        IAdminRuntimeConfig adminConfig)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _adminConfig = adminConfig;
        
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

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        await ExecuteRefreshMaxOdds(stoppingToken);

        // Un solo bucle: en XX:15 UTC primero datos nuevos, luego negocio con ese dataset (sin desincronización)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var minute = _adminConfig.UpdaterMinute ?? 15;
                var delay = GetDelayUntilNextXXMinuteUtc(minute);
                if (delay > TimeSpan.Zero)
                {
                    _logger.Debug("[UpdaterHostedService] :: Next run at XX:{1:D2} UTC in {0:F0}s", delay.TotalSeconds, minute);
                    await Task.Delay(delay, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested) break;

                var marketOpen = IsMarketOpen();

                await ExecuteUpdateAssets(marketOpen, stoppingToken);
                await ExecuteCheckBets(marketOpen, stoppingToken);
                await ExecuteCreateBets(marketOpen, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[UpdaterHostedService] :: Error in updater loop");
            }
        }
    }

    /// <summary>Espera hasta la próxima XX:MM UTC y devuelve el TimeSpan a esperar.</summary>
    private static TimeSpan GetDelayUntilNextXXMinuteUtc(int minute)
    {
        var now = DateTime.UtcNow;
        var m = Math.Clamp(minute, 0, 59);
        var currentHourXX = new DateTime(now.Year, now.Month, now.Day, now.Hour, m, 0, DateTimeKind.Utc);
        var next = now <= currentHourXX ? currentHourXX : currentHourXX.AddHours(1);
        var delay = next - now;
        return delay.TotalMilliseconds > 0 ? delay : TimeSpan.Zero;
    }

    private bool IsMarketOpen()
    {
        var nyTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _nyZone);
        var open = new TimeSpan(9, 30, 0);
        var close = new TimeSpan(16, 0, 0);
        return nyTime.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
            && nyTime.TimeOfDay >= open
            && nyTime.TimeOfDay <= close;
    }

    private async Task ExecuteRefreshMaxOdds(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var updaterService = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
            _logger.Debug("[UpdaterHostedService] :: Populating max odds from database at startup");
            await updaterService.RefreshMaxOddsFromDatabaseAsync(cancellationToken);
            _logger.Debug("[UpdaterHostedService] :: Max odds populated");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[UpdaterHostedService] :: RefreshMaxOdds at startup failed (Trends may be empty until CreateBets runs)");
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
}
