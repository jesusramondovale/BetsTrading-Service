namespace BetsTrading.Domain.Entities;

public class PaymentData
{
    private PaymentData() { }

    public PaymentData(string userId, string paymentIntentId, double coins, string currency, double amount, bool isPaid, string paymentMethod)
    {
        UserId = userId;
        PaymentIntentId = paymentIntentId;
        Coins = coins;
        Currency = currency;
        Amount = amount;
        ExecutedAt = DateTime.UtcNow;
        IsPaid = isPaid;
        PaymentMethod = paymentMethod;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string PaymentIntentId { get; private set; } = string.Empty;
    public double Coins { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public double Amount { get; private set; }
    public DateTime ExecutedAt { get; private set; }
    public bool IsPaid { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
}
