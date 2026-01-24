using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IPriceBetRepository : IRepository<PriceBet>
{
    Task<PriceBet?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PriceBet>> GetUserPriceBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<PriceBet?> GetByTickerAndEndDateAsync(string ticker, DateTime endDate, CancellationToken cancellationToken = default);
}
