using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class FinancialAssetRepository : Repository<FinancialAsset>, IFinancialAssetRepository
{
    public FinancialAssetRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<FinancialAsset?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<FinancialAsset?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.Ticker == ticker, cancellationToken);
    }
}
