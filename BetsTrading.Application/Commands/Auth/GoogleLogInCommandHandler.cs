using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Services;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleLogInCommandHandler : IRequestHandler<GoogleLogInCommand, GoogleLogInResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;

    public GoogleLogInCommandHandler(IUnitOfWork unitOfWork, IJwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
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

            var jwtToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Fullname);

            return new GoogleLogInResult
            {
                Success = true,
                Message = "Google LogIn SUCCESS",
                UserId = user.Id,
                JwtToken = jwtToken
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
