using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Info;

public class UploadProfilePicCommandHandler : IRequestHandler<UploadProfilePicCommand, UploadProfilePicResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public UploadProfilePicCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UploadProfilePicResult> Handle(UploadProfilePicCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var userId = request.GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return new UploadProfilePicResult
                {
                    Success = false,
                    Message = "User token not found"
                };
            }

            if (string.IsNullOrEmpty(request.ProfilePic))
            {
                return new UploadProfilePicResult
                {
                    Success = false,
                    Message = "Profile picture URL is required"
                };
            }

            // Check if user has active session
            if (!user.IsActive || user.TokenExpiration <= DateTime.UtcNow)
            {
                return new UploadProfilePicResult
                {
                    Success = false,
                    Message = "No active session or session expired"
                };
            }

            user.ProfilePic = request.ProfilePic;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UploadProfilePicResult
            {
                Success = true,
                Message = "Profile pic successfully updated!",
                UserId = user.Id
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new UploadProfilePicResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
