using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class WithdrawalMethodRepository : Repository<WithdrawalMethod>, IWithdrawalMethodRepository
{
    public WithdrawalMethodRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WithdrawalMethod>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WithdrawalMethod?> GetByUserIdAndLabelAsync(string userId, string label, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Label == label, cancellationToken);
    }

    public async Task<WithdrawalMethod?> GetByUserIdTypeAndLabelAsync(string userId, string type, string label, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Type == type && w.Label == label, cancellationToken);
    }
}
