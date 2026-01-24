using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class NewPasswordCommandHandler : IRequestHandler<NewPasswordCommand, NewPasswordResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public NewPasswordCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<NewPasswordResult> Handle(NewPasswordCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return new NewPasswordResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            {
                return new NewPasswordResult
                {
                    Success = false,
                    Message = "Password must be at least 12 characters"
                };
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.UpdatePassword(hashedPassword);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new NewPasswordResult
            {
                Success = true,
                Message = "Password created successfully"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new NewPasswordResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
