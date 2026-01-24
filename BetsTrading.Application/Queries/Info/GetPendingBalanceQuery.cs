using MediatR;

namespace BetsTrading.Application.Queries.Info;

public class GetPendingBalanceQuery : IRequest<GetPendingBalanceResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetPendingBalanceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? Balance { get; set; }
    public bool PasswordNotSet { get; set; }
}
