using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IDailyLoginStreakRepository : IRepository<DailyLoginStreak>
{
    Task<DailyLoginStreak?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
