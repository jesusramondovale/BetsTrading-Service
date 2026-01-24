using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRewardNonceRepository : IRepository<RewardNonce>
{
    Task<RewardNonce?> GetByNonceAsync(string nonce, CancellationToken cancellationToken = default);
    Task<IEnumerable<RewardNonce>> GetExpiredUnusedAsync(CancellationToken cancellationToken = default);
    Task<int> CountOutstandingByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
