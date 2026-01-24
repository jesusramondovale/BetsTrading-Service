using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.BetZones;

public class GetBetZonesQuery : IRequest<IEnumerable<BetZoneDto>>
{
    public string? Ticker { get; set; }
    public int? Timeframe { get; set; }
    public bool ActiveOnly { get; set; } = true;
}
