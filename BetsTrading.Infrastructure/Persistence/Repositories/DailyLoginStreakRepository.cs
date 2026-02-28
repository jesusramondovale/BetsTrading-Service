using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class DailyLoginStreakRepository : Repository<DailyLoginStreak>, IDailyLoginStreakRepository
{
    public DailyLoginStreakRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<DailyLoginStreak?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }
}
