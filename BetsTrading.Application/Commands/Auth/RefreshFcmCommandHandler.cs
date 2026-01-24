using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class RefreshFcmCommandHandler : IRequestHandler<RefreshFcmCommand, RefreshFcmResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public RefreshFcmCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RefreshFcmResult> Handle(RefreshFcmCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = request.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "User ID is required"
                };
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            var fcm = request.GetFcm();
            if (string.IsNullOrEmpty(fcm))
            {
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "FCM token is required"
                };
            }

            user.UpdateFcm(fcm);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RefreshFcmResult
            {
                Success = true,
                Message = "FCM token updated successfully"
            };
        }
        catch (Exception ex)
        {
            return new RefreshFcmResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
