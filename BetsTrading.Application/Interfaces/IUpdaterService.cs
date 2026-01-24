namespace BetsTrading.Application.Interfaces;

public interface IUpdaterService
{
    Task UpdateAssetsAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task CreateBetZonesAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task CheckBetsAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task RefreshTargetOddsAsync(CancellationToken cancellationToken = default);
    Task UpdateTrendsAsync(bool marketHours, CancellationToken cancellationToken = default);
}
