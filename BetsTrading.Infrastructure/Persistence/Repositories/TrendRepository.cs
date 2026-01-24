using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class TrendRepository : Repository<Trend>, ITrendRepository
{
    public TrendRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Trend?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Ticker == ticker, cancellationToken);
    }

    public new async Task<IEnumerable<Trend>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }
}
