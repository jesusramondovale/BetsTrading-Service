using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IRewardTransactionRepository : IRepository<RewardTransaction>
{
    Task<RewardTransaction?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RewardTransaction>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
