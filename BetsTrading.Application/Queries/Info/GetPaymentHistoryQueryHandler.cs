using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetPaymentHistoryQueryHandler : IRequestHandler<GetPaymentHistoryQuery, GetPaymentHistoryResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPaymentHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetPaymentHistoryResult> Handle(GetPaymentHistoryQuery request, CancellationToken cancellationToken)
    {
        var userExists = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (userExists == null)
        {
            return new GetPaymentHistoryResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        var payments = await _unitOfWork.PaymentData.GetByUserIdAsync(request.UserId, cancellationToken);

        var paymentDtos = payments.OrderByDescending(p => p.ExecutedAt).Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            UserId = p.UserId,
            PaymentIntentId = p.PaymentIntentId,
            Coins = p.Coins,
            Currency = p.Currency,
            Amount = p.Amount,
            ExecutedAt = p.ExecutedAt,
            IsPaid = p.IsPaid,
            PaymentMethod = p.PaymentMethod
        }).ToList();

        return new GetPaymentHistoryResult
        {
            Success = true,
            Payments = paymentDtos
        };
    }
}
