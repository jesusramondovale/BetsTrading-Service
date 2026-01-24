namespace BetsTrading.Domain.Entities;

public class WithdrawalData
{
    private WithdrawalData() { }

    public WithdrawalData(string userId, double coins, string currency, double amount, bool isPaid, string paymentMethod)
    {
        UserId = userId;
        Coins = coins;
        Currency = currency;
        Amount = amount;
        ExecutedAt = DateTime.UtcNow;
        IsPaid = isPaid;
        PaymentMethod = paymentMethod;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public double Coins { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public double Amount { get; private set; }
    public DateTime ExecutedAt { get; private set; }
    public bool IsPaid { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
}
