using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRaffleItemRepository : IRepository<RaffleItem>
{
    Task<RaffleItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
