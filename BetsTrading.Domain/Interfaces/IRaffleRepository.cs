using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRaffleRepository : IRepository<Raffle>
{
    Task<IEnumerable<Raffle>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
