using BetsTrading.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BetsTrading.Infrastructure.HostedServices;

public class RaffleDrawHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLogger _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public RaffleDrawHostedService(IServiceProvider serviceProvider, IApplicationLogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Debug("[RaffleDrawHostedService] :: Started. First run in 20 seconds.");
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _mutex.WaitAsync(stoppingToken);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var draw = scope.ServiceProvider.GetRequiredService<IRaffleDrawService>();
                await draw.ProcessDueRafflesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[RaffleDrawHostedService] :: Error in draw loop");
            }
            finally
            {
                _mutex.Release();
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
