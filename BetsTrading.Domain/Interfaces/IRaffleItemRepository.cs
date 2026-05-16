using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRaffleItemRepository : IRepository<RaffleItem>
{
    Task<RaffleItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RaffleItem>> GetDueItemsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task EnsureDefaultItemsAsync(CancellationToken cancellationToken = default);
}
