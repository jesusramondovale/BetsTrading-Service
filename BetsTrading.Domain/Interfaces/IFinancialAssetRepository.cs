using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IFinancialAssetRepository : IRepository<FinancialAsset>
{
    Task<FinancialAsset?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<FinancialAsset?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
}
