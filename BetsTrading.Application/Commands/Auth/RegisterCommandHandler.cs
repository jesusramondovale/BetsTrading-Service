using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Services;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Auth;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IApplicationLogger _logger;
    private const int SESSION_EXP_DAYS = 15;

    public RegisterCommandHandler(
        IUnitOfWork unitOfWork, 
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Validate verification code if not Google quick mode
            if (!request.GoogleQuickMode)
            {
                if (string.IsNullOrWhiteSpace(request.EmailCode))
                {
                    return new RegisterResult
                    {
                        Success = false,
                        Message = "Email verification code is required"
                    };
                }

                var verification = await _unitOfWork.VerificationCodes.GetByEmailAndCodeAsync(
                    request.Email, 
                    request.EmailCode, 
                    cancellationToken);

                if (verification == null)
                {
                    return new RegisterResult
                    {
                        Success = false,
                        Message = "Invalid or expired verification code"
                    };
                }
            }

            // Check if user already exists
            var existingUser = await _unitOfWork.Users.GetByEmailOrUsernameAsync(
                request.Email, 
                cancellationToken);

            if (existingUser != null)
            {
                return new RegisterResult
                {
                    Success = false,
                    Message = "Username, email or ID already exists"
                };
            }

            // Hash password
            string hashedPassword = !string.IsNullOrEmpty(request.Password)
                ? BCrypt.Net.BCrypt.HashPassword(request.Password)
                : "nullPassword";

            // Generate user ID
            string userId = request.Token ?? Guid.NewGuid().ToString();

            // Create user
            var newUser = new User(
                id: userId,
                fcm: request.Fcm,
                fullname: request.FullName,
                password: hashedPassword,
                country: request.Country ?? "",
                gender: request.Gender ?? "-",
                email: request.Email,
                birthday: request.Birthday ?? DateTime.UtcNow,
                username: request.Username,
                profilePic: request.ProfilePic,
                points: 0.0,
                creditCard: request.CreditCard ?? "nullCreditCard"
            );

            newUser.IsActive = true;
            newUser.TokenExpiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);

            await _unitOfWork.Users.AddAsync(newUser, cancellationToken);

            // Mark verification code as verified
            if (!request.GoogleQuickMode)
            {
                var verification = await _unitOfWork.VerificationCodes.GetByEmailAndCodeAsync(
                    request.Email, 
                    request.EmailCode!, 
                    cancellationToken);
                
                if (verification != null)
                {
                    verification.MarkAsVerified();
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Generate JWT token
            var jwtToken = _jwtTokenService.GenerateToken(newUser.Id, newUser.Email, newUser.Fullname, 12);

            // Send welcome email (simplified)
            try
            {
                await _emailService.SendEmailAsync(
                    to: request.Email,
                    subject: "Welcome to BetsTrading",
                    body: $"Welcome {request.FullName}! Your registration was successful."
                );
            }
            catch (Exception ex)
            {
                _logger.Warning("[AUTH] :: Register :: Failed to send welcome email: {0}", ex.Message);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.Information("[AUTH] :: Register :: Success with user ID: {0}", newUser.Id);

            return new RegisterResult
            {
                Success = true,
                Message = "Registration successful!",
                UserId = newUser.Id,
                JwtToken = jwtToken
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[AUTH] :: Register :: Error: {0}", ex.Message);
            return new RegisterResult
            {
                Success = false,
                Message = "Internal server error",
            };
        }
    }
}
