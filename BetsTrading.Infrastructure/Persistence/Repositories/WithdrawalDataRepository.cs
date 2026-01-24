using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class WithdrawalDataRepository : Repository<WithdrawalData>, IWithdrawalDataRepository
{
    public WithdrawalDataRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WithdrawalData>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.ExecutedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WithdrawalData>> GetPendingWithdrawalsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(w => !w.IsPaid)
            .OrderByDescending(w => w.ExecutedAt)
            .ToListAsync(cancellationToken);
    }
}
