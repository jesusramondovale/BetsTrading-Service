using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class SendCodeCommandHandler : IRequestHandler<SendCodeCommand, SendCodeResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IApplicationLogger _logger;

    public SendCodeCommandHandler(IUnitOfWork unitOfWork, IEmailService emailService, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<SendCodeResult> Handle(SendCodeCommand request, CancellationToken cancellationToken)
    {
        // Check if user already exists
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            return new SendCodeResult
            {
                Success = false,
                Message = "Email already exists"
            };
        }

        // Generate verification code
        var random = new Random();
        string code = random.Next(100000, 999999).ToString();

        var verificationCode = new VerificationCode(
            request.Email,
            code,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(10)
        );

        // Remove old unverified codes for this email
        var oldCodes = await _unitOfWork.VerificationCodes.GetUnverifiedByEmailAsync(request.Email, cancellationToken);
        foreach (var oldCode in oldCodes)
        {
            _unitOfWork.VerificationCodes.Remove(oldCode);
        }

        // Add new verification code
        await _unitOfWork.VerificationCodes.AddAsync(verificationCode, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send email (simplified - will need proper localization)
        try
        {
            await _emailService.SendEmailAsync(
                to: request.Email,
                subject: "Verification Code",
                body: $"Your verification code is: {code}"
            );

            _logger.Debug("[AUTH] :: SendCode :: Verification code sent for {0}", request.Email);

            return new SendCodeResult
            {
                Success = true,
                Message = "Verification code sent successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[AUTH] :: SendCode :: Error sending email");
            return new SendCodeResult
            {
                Success = false,
                Message = "Failed to send verification code"
            };
        }
    }
}
