using MediatR;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class DeleteWithdrawalMethodCommand : IRequest<DeleteWithdrawalMethodResult>
{
    public string? UserId { get; set; }
    public string Label { get; set; } = string.Empty;

    public string GetUserId() => UserId ?? string.Empty;
}

public class DeleteWithdrawalMethodResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
