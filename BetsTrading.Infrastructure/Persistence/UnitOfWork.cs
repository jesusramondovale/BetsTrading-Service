using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence.Repositories;

namespace BetsTrading.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IBetRepository? _bets;
    private IUserRepository? _users;
    private IBetZoneRepository? _betZones;
    private IFinancialAssetRepository? _financialAssets;
    private IVerificationCodeRepository? _verificationCodes;
    private IPaymentDataRepository? _paymentData;
    private IWithdrawalDataRepository? _withdrawalData;
    private IRewardNonceRepository? _rewardNonces;
    private IRewardTransactionRepository? _rewardTransactions;
    private IAssetCandleRepository? _assetCandles;
    private IAssetCandleUsdRepository? _assetCandlesUSD;
    private ITrendRepository? _trends;
    private IBetZoneUsdRepository? _betZonesUSD;
    private IPriceBetRepository? _priceBets;
    private IPriceBetUsdRepository? _priceBetsUSD;
    private IFavoriteRepository? _favorites;
    private IWithdrawalMethodRepository? _withdrawalMethods;
    private IRaffleRepository? _raffles;
    private IRaffleItemRepository? _raffleItems;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IBetRepository Bets => _bets ??= new BetRepository(_context);
    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IBetZoneRepository BetZones => _betZones ??= new BetZoneRepository(_context);
    public IFinancialAssetRepository FinancialAssets => _financialAssets ??= new FinancialAssetRepository(_context);
    public IVerificationCodeRepository VerificationCodes => _verificationCodes ??= new VerificationCodeRepository(_context);
    public IPaymentDataRepository PaymentData => _paymentData ??= new PaymentDataRepository(_context);
    public IWithdrawalDataRepository WithdrawalData => _withdrawalData ??= new WithdrawalDataRepository(_context);
    public IRewardNonceRepository RewardNonces => _rewardNonces ??= new RewardNonceRepository(_context);
    public IRewardTransactionRepository RewardTransactions => _rewardTransactions ??= new RewardTransactionRepository(_context);
    public IAssetCandleRepository AssetCandles => _assetCandles ??= new AssetCandleRepository(_context);
    public IAssetCandleUsdRepository AssetCandlesUSD => _assetCandlesUSD ??= new AssetCandleUsdRepository(_context);
    public ITrendRepository Trends => _trends ??= new TrendRepository(_context);
    public IBetZoneUsdRepository BetZonesUSD => _betZonesUSD ??= new BetZoneUsdRepository(_context);
    public IPriceBetRepository PriceBets => _priceBets ??= new PriceBetRepository(_context);
    public IPriceBetUsdRepository PriceBetsUSD => _priceBetsUSD ??= new PriceBetUsdRepository(_context);
    public IFavoriteRepository Favorites => _favorites ??= new FavoriteRepository(_context);
    public IWithdrawalMethodRepository WithdrawalMethods => _withdrawalMethods ??= new WithdrawalMethodRepository(_context);
    public IRaffleRepository Raffles => _raffles ??= new RaffleRepository(_context);
    public IRaffleItemRepository RaffleItems => _raffleItems ??= new RaffleItemRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public AppDbContext GetDbContext()
    {
        return _context;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
