using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTopUsersQuery : IRequest<GetTopUsersResult>
{
    public string UserId { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;
}

public class GetTopUsersResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<UserRankingDto> Users { get; set; } = new();
}
