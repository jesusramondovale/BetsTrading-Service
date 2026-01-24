using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Auth;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public ChangePasswordCommandHandler(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ChangePasswordResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            _logger.Warning("[AUTH] :: ChangePassword :: User not found: {0}", request.UserId);
            return new ChangePasswordResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
        {
            _logger.Warning("[AUTH] :: ChangePassword :: Invalid current password for user: {0}", request.UserId);
            return new ChangePasswordResult
            {
                Success = false,
                Message = "Current password is incorrect"
            };
        }

        // Update password
        string hashedNewPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatePassword(hashedNewPassword);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.Information("[AUTH] :: ChangePassword :: Success for user: {0}", request.UserId);

        return new ChangePasswordResult
        {
            Success = true,
            Message = "Password changed successfully"
        };
    }
}
