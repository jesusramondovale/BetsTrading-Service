using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetUserBetsQuery : IRequest<IEnumerable<BetDto>>
{
    public string UserId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; } = false;
}
