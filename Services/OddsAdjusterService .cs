using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetsTrading_Service.Services
{
  public sealed record OddsAdjusterOptions
  {
    public TimeSpan AdjustRefreshTime { get; init; } = TimeSpan.FromSeconds(4);
  }

  public class OddsAdjusterService : BackgroundService
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OddsAdjusterService> _logger;
    private readonly IOptionsMonitor<OddsAdjusterOptions> _options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public OddsAdjusterService(IServiceProvider serviceProvider, ILogger<OddsAdjusterService> logger, IOptionsMonitor<OddsAdjusterOptions> options)
    {
      _serviceProvider = serviceProvider;
      _logger = logger;
      _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        await _mutex.WaitAsync(stoppingToken);
        try
        {
          using var scope = _serviceProvider.CreateScope();
          var updater = scope.ServiceProvider.GetRequiredService<UpdaterService>();
          await updater.RefreshTargetOddsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error executing AdjustTargetOdds().");
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
        catch
        {
        }
      }
    }
  }
}
