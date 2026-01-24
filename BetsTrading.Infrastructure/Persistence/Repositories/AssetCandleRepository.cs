using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class AssetCandleRepository : Repository<AssetCandle>, IAssetCandleRepository
{
    public AssetCandleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AssetCandle?> GetLatestCandleAsync(int assetId, string interval, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .OrderByDescending(c => c.DateTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AssetCandle>> GetCandlesByAssetAsync(int assetId, string interval, int count, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .OrderByDescending(c => c.DateTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestDateTimeAsync(int assetId, string interval, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .MaxAsync(c => (DateTime?)c.DateTime, cancellationToken);
    }

    public async Task<IEnumerable<AssetCandle>> GetCandlesByDateRangeAsync(int assetId, string interval, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && 
                       c.Interval == interval && 
                       c.DateTime >= startDate && 
                       c.DateTime < endDate)
            .ToListAsync(cancellationToken);
    }
}

public class AssetCandleUsdRepository : Repository<AssetCandleUSD>, IAssetCandleUsdRepository
{
    public AssetCandleUsdRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AssetCandleUSD?> GetLatestCandleAsync(int assetId, string interval, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .OrderByDescending(c => c.DateTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AssetCandleUSD>> GetCandlesByAssetAsync(int assetId, string interval, int count, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && c.Interval == interval)
            .OrderByDescending(c => c.DateTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AssetCandleUSD>> GetCandlesByDateRangeAsync(int assetId, string interval, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.AssetId == assetId && 
                       c.Interval == interval && 
                       c.DateTime >= startDate && 
                       c.DateTime < endDate)
            .ToListAsync(cancellationToken);
    }
}
