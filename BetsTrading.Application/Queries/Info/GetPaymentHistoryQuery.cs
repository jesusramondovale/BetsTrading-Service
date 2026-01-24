using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetPaymentHistoryQuery : IRequest<GetPaymentHistoryResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetPaymentHistoryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<PaymentHistoryDto> Payments { get; set; } = new();
}
