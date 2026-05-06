using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface ICopyTradingSubscriptionRepository : IRepository<CopyTradingSubscription>
{
    Task<CopyTradingSubscription?> GetByFollowerAndTargetAsync(
        string followerUserId,
        string targetUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CopyTradingSubscription>> GetActiveByTargetUserIdAsync(
        string targetUserId,
        CancellationToken cancellationToken = default);
}
