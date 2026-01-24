using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetBetZoneQuery : IRequest<BetZoneDto?>
{
    public int BetId { get; set; }
    public string Currency { get; set; } = "EUR";
}
