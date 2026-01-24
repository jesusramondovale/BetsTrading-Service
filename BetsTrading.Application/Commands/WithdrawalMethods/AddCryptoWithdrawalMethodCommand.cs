using MediatR;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class AddCryptoWithdrawalMethodCommand : IRequest<AddWithdrawalMethodResult>
{
    public string? UserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Memo { get; set; }

    public string GetUserId() => UserId ?? string.Empty;
}
