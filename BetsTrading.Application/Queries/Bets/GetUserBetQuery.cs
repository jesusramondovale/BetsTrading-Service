using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetUserBetQuery : IRequest<BetDto?>
{
    public string UserId { get; set; } = string.Empty;
    public int BetId { get; set; }
    public string Currency { get; set; } = "EUR";
}
