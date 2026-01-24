using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class DeleteWithdrawalMethodCommandHandler : IRequestHandler<DeleteWithdrawalMethodCommand, DeleteWithdrawalMethodResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWithdrawalMethodCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteWithdrawalMethodResult> Handle(DeleteWithdrawalMethodCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var userExists = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (userExists == null)
            {
                return new DeleteWithdrawalMethodResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            var method = await _unitOfWork.WithdrawalMethods.GetByUserIdAndLabelAsync(request.UserId, request.Label, cancellationToken);
            if (method == null)
            {
                return new DeleteWithdrawalMethodResult
                {
                    Success = false,
                    Message = $"Retire option with label {request.Label} not found for user {request.UserId}"
                };
            }

            _unitOfWork.WithdrawalMethods.Remove(method);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new DeleteWithdrawalMethodResult
            {
                Success = true,
                Message = "Withdrawal method deleted successfully"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new DeleteWithdrawalMethodResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
