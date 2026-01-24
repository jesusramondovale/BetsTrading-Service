using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleSignInCommandHandler : IRequestHandler<GoogleSignInCommand, GoogleSignInResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;
    private const int SESSION_EXP_DAYS = 15;

    public GoogleSignInCommandHandler(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GoogleSignInResult> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.Id, cancellationToken);
        
        if (user == null)
        {
            _logger.Warning("[AUTH] :: GoogleSignIn :: User not found: {0}", request.Id);
            return new GoogleSignInResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        // Update session
        user.UpdateSession();
        
        if (!string.IsNullOrEmpty(request.Fcm))
        {
            user.UpdateFcm(request.Fcm);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.Information("[AUTH] :: GoogleSignIn :: Success for user: {0}", user.Id);

        return new GoogleSignInResult
        {
            Success = true,
            Message = "Google login successful",
            UserId = user.Id
        };
    }
}
