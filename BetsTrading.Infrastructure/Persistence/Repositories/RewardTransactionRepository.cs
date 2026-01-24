using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class RewardTransactionRepository : Repository<RewardTransaction>, IRewardTransactionRepository
{
    public RewardTransactionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<RewardTransaction?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.TransactionId == transactionId, cancellationToken);
    }

    public async Task<bool> ExistsByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(r => r.TransactionId == transactionId, cancellationToken);
    }

    public async Task<IEnumerable<RewardTransaction>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
