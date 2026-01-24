using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.HostedServices;

public sealed record OddsAdjusterOptions
{
    public TimeSpan AdjustRefreshTime { get; init; } = TimeSpan.FromSeconds(4);
}

public class OddsAdjusterHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLogger _logger;
    private readonly IOptionsMonitor<OddsAdjusterOptions> _options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public OddsAdjusterHostedService(
        IServiceProvider serviceProvider,
        IApplicationLogger logger,
        IOptionsMonitor<OddsAdjusterOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Debug("[OddsAdjusterHostedService] :: Service started. Waiting 10 seconds before first execution to allow API to be ready...");
        
        // Esperar 10 segundos antes de la primera ejecución para que la API esté lista
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _mutex.WaitAsync(stoppingToken);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var updater = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
                await updater.RefreshTargetOddsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[OddsAdjusterHostedService] :: Error executing RefreshTargetOdds");
            }
            finally
            {
                _mutex.Release();
            }

            var wait = _options.CurrentValue.AdjustRefreshTime;
            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }
    }
}
