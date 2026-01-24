using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Queries.Info;

public class GetUserInfoQueryHandler : IRequestHandler<GetUserInfoQuery, UserInfoDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public GetUserInfoQueryHandler(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UserInfoDto?> Handle(GetUserInfoQuery request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            _logger.Warning("[INFO] :: UserInfo :: User not found for ID: {0}", request.UserId);
            return null;
        }

        _logger.Debug("[INFO] :: UserInfo :: Success on ID: {0}", request.UserId);

        return new UserInfoDto
        {
            Username = user.Username,
            IsVerified = user.IsVerified,
            Email = user.Email,
            Birthday = user.Birthday,
            Fullname = user.Fullname,
            Country = user.Country,
            LastSession = user.LastSession,
            ProfilePic = user.ProfilePic,
            Points = user.Points
        };
    }
}
