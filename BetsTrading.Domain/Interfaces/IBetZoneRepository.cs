using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IBetZoneRepository : IRepository<BetZone>
{
    Task<BetZone?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BetZone>> GetActiveBetZonesByTickerAsync(string ticker, int timeframe, CancellationToken cancellationToken = default);
    Task<BetZone?> GetBetZoneByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<int>> GetActiveBetZoneIdsByDateRangeAsync(DateTime startDate, DateTime endDate, bool marketHours, CancellationToken cancellationToken = default);
    Task<IEnumerable<BetZone>> GetActiveFutureBetZonesAsync(DateTime beforeDate, CancellationToken cancellationToken = default);
    Task<int> DeactivateZonesByTickerAsync(string ticker, CancellationToken cancellationToken = default);
}
