using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetHistoricUserBetsQuery : IRequest<IEnumerable<BetDto>>
{
    public string UserId { get; set; } = string.Empty;
}
