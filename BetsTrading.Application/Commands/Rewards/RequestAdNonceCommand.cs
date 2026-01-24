using MediatR;

namespace BetsTrading.Application.Commands.Rewards;

public class RequestAdNonceCommand : IRequest<RequestAdNonceResult>
{
    public string UserId { get; set; } = string.Empty;
    public string AdUnitId { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public int? Coins { get; set; }
}

public class RequestAdNonceResult
{
    public bool Success { get; set; }
    public string? Nonce { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
}
