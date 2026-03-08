using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Info;

public class SetUserPrivateCommandHandler : IRequestHandler<SetUserPrivateCommand, SetUserPrivateResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public SetUserPrivateCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SetUserPrivateResult> Handle(SetUserPrivateCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var userId = request.GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return new SetUserPrivateResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            if (!user.IsActive || user.TokenExpiration <= DateTime.UtcNow)
            {
                return new SetUserPrivateResult
                {
                    Success = false,
                    Message = "No active session or session expired"
                };
            }

            user.IsPrivate = request.IsPrivate;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new SetUserPrivateResult
            {
                Success = true,
                Message = request.IsPrivate ? "Private mode enabled" : "Private mode disabled"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new SetUserPrivateResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
