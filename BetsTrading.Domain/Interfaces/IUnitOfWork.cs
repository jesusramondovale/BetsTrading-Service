namespace BetsTrading.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IBetRepository Bets { get; }
    IUserRepository Users { get; }
    IBetZoneRepository BetZones { get; }
    IFinancialAssetRepository FinancialAssets { get; }
    IVerificationCodeRepository VerificationCodes { get; }
    IPaymentDataRepository PaymentData { get; }
    IWithdrawalDataRepository WithdrawalData { get; }
    IRewardNonceRepository RewardNonces { get; }
    IRewardTransactionRepository RewardTransactions { get; }
    IAssetCandleRepository AssetCandles { get; }
    IAssetCandleUsdRepository AssetCandlesUSD { get; }
    ITrendRepository Trends { get; }
    IBetZoneUsdRepository BetZonesUSD { get; }
    IPriceBetRepository PriceBets { get; }
    IPriceBetUsdRepository PriceBetsUSD { get; }
    IFavoriteRepository Favorites { get; }
    IWithdrawalMethodRepository WithdrawalMethods { get; }
    IRaffleRepository Raffles { get; }
    IRaffleItemRepository RaffleItems { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
