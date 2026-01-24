using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class BetZoneRepository : Repository<BetZone>, IBetZoneRepository
{
    public BetZoneRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BetZone>> GetActiveBetZonesByTickerAsync(string ticker, int timeframe, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(bz => bz.Ticker == ticker && bz.Active);
        
        if (timeframe > 0)
        {
            query = query.Where(bz => bz.Timeframe == timeframe);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public new async Task<BetZone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<BetZone?> GetBetZoneByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(bz => bz.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<int>> GetActiveBetZoneIdsByDateRangeAsync(DateTime startDate, DateTime endDate, bool marketHours, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _dbSet.Where(bz => now >= bz.StartDate && now <= bz.EndDate && bz.Active);

        if (!marketHours)
        {
            // Filtrar solo cryptos y forex
            var cryptoForexTickers = _context.Set<FinancialAsset>()
                .Where(a => a.Group.ToLower() == "cryptos" || a.Group.ToLower() == "forex")
                .Select(a => a.Ticker);

            query = query.Where(bz => cryptoForexTickers.Contains(bz.Ticker));
        }

        return await query.Select(bz => bz.Id).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<BetZone>> GetActiveFutureBetZonesAsync(DateTime beforeDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(bz => bz.Active && bz.StartDate > beforeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeactivateZonesByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(bz => bz.Ticker == ticker && bz.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(bz => bz.Active, _ => false), cancellationToken);
    }
}
