using MediatR;

namespace BetsTrading.Application.Commands.Payments;

public class CreatePaymentIntentCommand : IRequest<CreatePaymentIntentResult>
{
    public string UserId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "eur";
    public int Coins { get; set; }
}

public class CreatePaymentIntentResult
{
    public bool Success { get; set; }
    public string? ClientSecret { get; set; }
    public string? Message { get; set; }
}
