using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTopUsersQueryHandler : IRequestHandler<GetTopUsersQuery, GetTopUsersResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTopUsersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetTopUsersResult> Handle(GetTopUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _unitOfWork.Users.GetTopUsersByPointsAsync(request.Limit, cancellationToken);

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
