using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class RewardNonceRepository : Repository<RewardNonce>, IRewardNonceRepository
{
    public RewardNonceRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<RewardNonce?> GetByNonceAsync(string nonce, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.Nonce == nonce, cancellationToken);
    }

    public async Task<IEnumerable<RewardNonce>> GetExpiredUnusedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(r => r.ExpiresAt < now && !r.Used)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountOutstandingByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .CountAsync(r => r.UserId == userId && !r.Used && r.ExpiresAt >= now, cancellationToken);
    }
}
