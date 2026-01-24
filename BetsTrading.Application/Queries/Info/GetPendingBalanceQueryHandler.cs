using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Info;

public class GetPendingBalanceQueryHandler : IRequestHandler<GetPendingBalanceQuery, GetPendingBalanceResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPendingBalanceQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetPendingBalanceResult> Handle(GetPendingBalanceQuery request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return new GetPendingBalanceResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        // Check if password is not set
        if (string.IsNullOrEmpty(user.Password) || user.Password == "nullPassword" || user.Password.Length < 12)
        {
            return new GetPendingBalanceResult
            {
                Success = true,
                Message = "Password not set",
                PasswordNotSet = true
            };
        }

        return new GetPendingBalanceResult
        {
            Success = true,
            Balance = user.PendingBalance
        };
    }
}
