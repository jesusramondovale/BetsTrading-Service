using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Services;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationLogger _logger;
    private const int SESSION_EXP_DAYS = 15;

    public LoginCommandHandler(IUnitOfWork unitOfWork, IJwtTokenService jwtTokenService, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByEmailOrUsernameAsync(request.Username, cancellationToken);
        
        if (user == null)
        {
            _logger.Warning("[AUTH] :: Login :: User not found: {0}", request.Username);
            return new LoginResult
            {
                Success = false,
                Message = "Invalid username or password"
            };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            user.RecordFailedLoginAttempt();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.Warning("[AUTH] :: Login :: Invalid password for user: {0}", request.Username);
            return new LoginResult
            {
                Success = false,
                Message = "Invalid username or password"
            };
        }

        // Update session
        user.UpdateSession();
        
        if (!string.IsNullOrEmpty(request.Fcm))
        {
            user.UpdateFcm(request.Fcm);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var jwtToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Fullname, 12);

        _logger.Information("[AUTH] :: Login :: Success for user: {0}", user.Id);

        return new LoginResult
        {
            Success = true,
            Message = "Login successful",
            UserId = user.Id,
            JwtToken = jwtToken
        };
    }
}
