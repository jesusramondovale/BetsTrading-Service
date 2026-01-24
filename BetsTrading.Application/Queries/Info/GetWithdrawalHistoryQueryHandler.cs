using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetWithdrawalHistoryQueryHandler : IRequestHandler<GetWithdrawalHistoryQuery, GetWithdrawalHistoryResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetWithdrawalHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetWithdrawalHistoryResult> Handle(GetWithdrawalHistoryQuery request, CancellationToken cancellationToken)
    {
        var userExists = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (userExists == null)
        {
            return new GetWithdrawalHistoryResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        var withdrawals = await _unitOfWork.WithdrawalData.GetByUserIdAsync(request.UserId, cancellationToken);

        var withdrawalDtos = withdrawals.OrderByDescending(w => w.ExecutedAt).Select(w => new WithdrawalHistoryDto
        {
            Id = w.Id,
            UserId = w.UserId,
            Coins = w.Coins,
            Currency = w.Currency,
            Amount = w.Amount,
            ExecutedAt = w.ExecutedAt,
            IsPaid = w.IsPaid,
            PaymentMethod = w.PaymentMethod
        }).ToList();

        return new GetWithdrawalHistoryResult
        {
            Success = true,
            Withdrawals = withdrawalDtos
        };
    }
}
