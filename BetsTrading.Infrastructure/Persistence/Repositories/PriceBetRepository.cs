using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class PriceBetRepository : Repository<PriceBet>, IPriceBetRepository
{
    public PriceBetRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<PriceBet?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<PriceBet>> GetUserPriceBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(pb => pb.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(pb => !pb.Archived);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PriceBet?> GetByTickerAndEndDateAsync(string ticker, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pb => pb.Ticker == ticker && pb.EndDate == endDate, cancellationToken);
    }
}
