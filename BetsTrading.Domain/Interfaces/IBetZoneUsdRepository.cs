using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IBetZoneUsdRepository : IRepository<BetZoneUSD>
{
    Task<BetZoneUSD?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BetZoneUSD>> GetActiveBetZonesByTickerAsync(string ticker, int timeframe, CancellationToken cancellationToken = default);
    Task<IEnumerable<int>> GetActiveBetZoneIdsByDateRangeAsync(DateTime startDate, DateTime endDate, bool marketHours, CancellationToken cancellationToken = default);
    Task<int> DeactivateZonesByTickerAsync(string ticker, CancellationToken cancellationToken = default);
}
