namespace BetsTrading.Application.Interfaces;

public sealed class PaymentFulfillmentResult
{
    public bool Success { get; init; }
    public bool AlreadyProcessed { get; init; }
    public double CoinsCredited { get; init; }
    public string? Message { get; init; }
}

public interface IStripePaymentFulfillmentService
{
    Task<PaymentFulfillmentResult> TryFulfillPaymentIntentAsync(
        string paymentIntentId,
        string? expectedUserId = null,
        CancellationToken cancellationToken = default);
}
