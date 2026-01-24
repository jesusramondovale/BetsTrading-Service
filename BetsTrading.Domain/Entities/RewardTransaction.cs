namespace BetsTrading.Domain.Entities;

public class RewardTransaction
{
    private RewardTransaction() { }

    public RewardTransaction(
        string transactionId,
        string userId,
        decimal coins,
        string? adUnitId,
        string? rewardItem,
        double? rewardAmountRaw,
        int? ssvKeyId,
        string? rawQuery)
    {
        Id = Guid.NewGuid();
        TransactionId = transactionId;
        UserId = userId;
        Coins = coins;
        AdUnitId = adUnitId;
        RewardItem = rewardItem;
        RewardAmountRaw = rewardAmountRaw;
        SsvKeyId = ssvKeyId;
        RawQuery = rawQuery;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string TransactionId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public decimal Coins { get; private set; }
    public string? AdUnitId { get; private set; }
    public string? RewardItem { get; private set; }
    public double? RewardAmountRaw { get; private set; }
    public int? SsvKeyId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? RawQuery { get; private set; }
}
