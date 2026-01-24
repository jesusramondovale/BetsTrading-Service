using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetBetZonesQuery : IRequest<GetBetZonesResult>
{
    public string Ticker { get; set; } = string.Empty;
    public int Timeframe { get; set; }
    public string Currency { get; set; } = "EUR";
}

public class GetBetZonesResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<BetZoneDto> BetZones { get; set; } = new();
}
