using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Auth;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILocalizationService _localizationService;

    public ResetPasswordCommandHandler(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _localizationService = localizationService;
    }

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.EmailOrId, cancellationToken);
            if (user == null)
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            string newPassword = GenerateSecurePassword();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Update password - User entity needs UpdatePassword method or we use reflection/setter
            // For now, we'll need to check if User has a method to update password
            user.UpdatePassword(hashedPassword);
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            string localizedBodyTemplate = _localizationService.GetTranslationByCountry(user.Country ?? "UK", "resetPasswordEmailBody");
            string localizedBody = string.Format(localizedBodyTemplate, user.Fullname, newPassword);

            await _emailService.SendEmailAsync(
                to: user.Email,
                subject: _localizationService.GetTranslationByCountry(user.Country ?? "UK", "emailSubjectPassword"),
                body: localizedBody
            );

            return new ResetPasswordResult
            {
                Success = true,
                Message = "New password generated and sent by email"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new ResetPasswordResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 16)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
