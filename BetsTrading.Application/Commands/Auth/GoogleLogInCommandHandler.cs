using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleLogInCommandHandler : IRequestHandler<GoogleLogInCommand, GoogleLogInResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GoogleLogInCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GoogleLogInResult> Handle(GoogleLogInCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = request.GetUserId();
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                return new GoogleLogInResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            user.UpdateSession();
            user.IsActive = true;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new GoogleLogInResult
            {
                Success = true,
                Message = "Google LogIn SUCCESS",
                UserId = user.Id
            };
        }
        catch (Exception ex)
        {
            return new GoogleLogInResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
