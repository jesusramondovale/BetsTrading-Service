using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IAssetCandleRepository : IRepository<AssetCandle>
{
    Task<AssetCandle?> GetLatestCandleAsync(int assetId, string interval, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssetCandle>> GetCandlesByAssetAsync(int assetId, string interval, int count, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLatestDateTimeAsync(int assetId, string interval, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssetCandle>> GetCandlesByDateRangeAsync(int assetId, string interval, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

public interface IAssetCandleUsdRepository : IRepository<AssetCandleUSD>
{
    Task<AssetCandleUSD?> GetLatestCandleAsync(int assetId, string interval, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssetCandleUSD>> GetCandlesByAssetAsync(int assetId, string interval, int count, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssetCandleUSD>> GetCandlesByDateRangeAsync(int assetId, string interval, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
