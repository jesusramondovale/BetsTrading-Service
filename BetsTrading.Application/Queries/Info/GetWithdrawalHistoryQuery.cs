using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetWithdrawalHistoryQuery : IRequest<GetWithdrawalHistoryResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetWithdrawalHistoryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<WithdrawalHistoryDto> Withdrawals { get; set; } = new();
}
