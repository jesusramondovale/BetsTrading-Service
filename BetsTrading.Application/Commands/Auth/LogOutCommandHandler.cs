using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class LogOutCommandHandler : IRequestHandler<LogOutCommand, LogOutResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public LogOutCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LogOutResult> Handle(LogOutCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
            {
                return new LogOutResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            user.LastSession = DateTime.UtcNow;
            user.IsActive = false;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new LogOutResult
            {
                Success = true,
                Message = "LogOut SUCCESS",
                UserId = user.Id
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new LogOutResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
