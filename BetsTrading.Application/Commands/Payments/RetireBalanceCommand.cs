using MediatR;

namespace BetsTrading.Application.Commands.Payments;

public class RetireBalanceCommand : IRequest<RetireBalanceResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Fcm { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public double CurrencyAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public double Coins { get; set; }
    public string Method { get; set; } = string.Empty;

    /// <summary>IP del cliente (X-Forwarded-For o RemoteIpAddress). Para geo en email y logs, como legacy.</summary>
    public string? ClientIp { get; set; }
}

public class RetireBalanceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
