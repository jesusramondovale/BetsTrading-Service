using MediatR;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class AddPaypalWithdrawalMethodCommand : IRequest<AddWithdrawalMethodResult>
{
    public string? UserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string GetUserId() => UserId ?? string.Empty;
}
