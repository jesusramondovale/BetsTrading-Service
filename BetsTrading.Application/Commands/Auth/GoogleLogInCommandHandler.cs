using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Services;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleLogInCommandHandler : IRequestHandler<GoogleLogInCommand, GoogleLogInResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IGoogleIdTokenValidator _googleIdTokenValidator;

    public GoogleLogInCommandHandler(
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IGoogleIdTokenValidator googleIdTokenValidator)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _googleIdTokenValidator = googleIdTokenValidator;
    }

    public async Task<GoogleLogInResult> Handle(GoogleLogInCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var googlePayload = await _googleIdTokenValidator.ValidateAsync(request.IdToken ?? "", cancellationToken);
            if (googlePayload == null)
            {
                return new GoogleLogInResult
                {
                    Success = false,
                    Message = "Invalid Google ID token"
                };
            }

            var userId = request.GetUserId();
            if (!string.Equals(userId, googlePayload.Subject, StringComparison.Ordinal))
            {
                return new GoogleLogInResult
                {
                    Success = false,
                    Message = "Google ID mismatch"
                };
            }

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
