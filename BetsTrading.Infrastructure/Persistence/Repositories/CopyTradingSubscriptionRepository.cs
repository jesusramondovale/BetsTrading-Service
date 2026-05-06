using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class CopyTradingSubscriptionRepository : Repository<CopyTradingSubscription>, ICopyTradingSubscriptionRepository
{
    public CopyTradingSubscriptionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<CopyTradingSubscription?> GetByFollowerAndTargetAsync(
        string followerUserId,
        string targetUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(
            x => x.FollowerUserId == followerUserId && x.TargetUserId == targetUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CopyTradingSubscription>> GetActiveByTargetUserIdAsync(
        string targetUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.TargetUserId == targetUserId && x.IsActive)
            .ToListAsync(cancellationToken);
    }
}
