using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IPriceBetUsdRepository : IRepository<PriceBetUSD>
{
    Task<PriceBetUSD?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PriceBetUSD>> GetUserPriceBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<PriceBetUSD?> GetByTickerAndEndDateAsync(string ticker, DateTime endDate, CancellationToken cancellationToken = default);
}
