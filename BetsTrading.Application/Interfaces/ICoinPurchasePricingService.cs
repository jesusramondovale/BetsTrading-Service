namespace BetsTrading.Application.Interfaces;

public sealed class CoinPurchasePackage
{
    public int Coins { get; init; }
    public double Price { get; init; }
    public string Currency { get; init; } = "eur";
}

public interface ICoinPurchasePricingService
{
    bool TryResolvePackage(string currency, int coins, long amountMinor, out CoinPurchasePackage package);
}
