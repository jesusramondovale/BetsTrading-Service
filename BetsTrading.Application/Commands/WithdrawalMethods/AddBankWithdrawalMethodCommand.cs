using MediatR;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class AddBankWithdrawalMethodCommand : IRequest<AddWithdrawalMethodResult>
{
    public string? UserId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Holder { get; set; } = string.Empty;
    public string? Bic { get; set; }

    public string GetUserId() => UserId ?? string.Empty;
}

public class AddWithdrawalMethodResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? Id { get; set; }
    public bool Created { get; set; }
    public bool Verified { get; set; }
}
