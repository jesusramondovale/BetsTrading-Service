using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTopUsersByCountryQueryHandler : IRequestHandler<GetTopUsersByCountryQuery, GetTopUsersResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTopUsersByCountryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetTopUsersResult> Handle(GetTopUsersByCountryQuery request, CancellationToken cancellationToken)
    {
        var countryCode = request.GetCountryCode();
        var users = await _unitOfWork.Users.GetTopUsersByCountryAsync(countryCode, request.Limit, cancellationToken);

        if (!users.Any())
        {
            return new GetTopUsersResult
            {
                Success = false,
                Message = "No users found"
            };
        }

        var userDtos = users.Select(u => new UserRankingDto
        {
            Id = u.Id,
            Fullname = u.Fullname,
            Username = u.Username,
            Points = u.Points,
            ProfilePic = u.ProfilePic,
            Country = u.Country
        }).ToList();

        return new GetTopUsersResult
        {
            Success = true,
            Users = userDtos
        };
    }
}
