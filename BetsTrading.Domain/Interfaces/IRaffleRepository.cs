using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRaffleRepository : IRepository<Raffle>
{
    Task<IEnumerable<Raffle>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasParticipatedAsync(string userId, int itemId, CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetParticipatedItemIdsForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Raffle>> GetEntriesByItemIdAsync(int itemId, CancellationToken cancellationToken = default);
    Task DeleteEntriesByItemIdAsync(int itemId, CancellationToken cancellationToken = default);
}
