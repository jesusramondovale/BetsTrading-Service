using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Queries.Auth;

public class IsLoggedInQueryHandler : IRequestHandler<IsLoggedInQuery, IsLoggedInResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public IsLoggedInQueryHandler(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IsLoggedInResult> Handle(IsLoggedInQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = request.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new IsLoggedInResult
                {
                    Success = false,
                    Message = "UserId or id is required"
                };
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.Warning("[IsLoggedIn] :: User not found: {UserId}", userId);
                return new IsLoggedInResult
                {
                    Success = false,
                    Message = "Token not found"
                };
            }

            // Check if password is not set
            if (string.IsNullOrEmpty(user.Password) || user.Password == "nullPassword" || user.Password.Length == 0)
            {
                _logger.Information("[IsLoggedIn] :: Session active but password not set on id {UserId}", userId);
                return new IsLoggedInResult
                {
                    Success = true,
                    Message = "Password not set",
                    PasswordNotSet = true,
                    UserId = user.Id
                };
            }

            // Check if session is active
            if (user.IsActive && user.TokenExpiration > DateTime.UtcNow)
            {
                _logger.Debug("[IsLoggedIn] :: Session active on id {UserId}", userId);
                return new IsLoggedInResult
                {
                    Success = true,
                    Message = "User is logged in",
                    IsLoggedIn = true,
                    UserId = user.Id
                };
            }
            else
            {
                _logger.Warning("[IsLoggedIn] :: Session inactive or expired on id {UserId}", userId);
                return new IsLoggedInResult
                {
                    Success = false,
                    Message = "No active session or session expired"
                };
            }
        }
        catch (Exception ex)
        {
            var userId = request.GetUserId();
            _logger.Error(ex, "[IsLoggedIn] :: Error checking login status for user {UserId}: {Error}", userId, ex.Message);
            return new IsLoggedInResult
            {
                Success = false,
                Message = "An error occurred while checking login status"
            };
        }
    }
}
