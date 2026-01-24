using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class PriceBetUsdRepository : Repository<PriceBetUSD>, IPriceBetUsdRepository
{
    public PriceBetUsdRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<PriceBetUSD?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<PriceBetUSD>> GetUserPriceBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(pb => pb.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(pb => !pb.Archived);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PriceBetUSD?> GetByTickerAndEndDateAsync(string ticker, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pb => pb.Ticker == ticker && pb.EndDate == endDate, cancellationToken);
    }
}
