using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class CreateBetCommand : IRequest<CreateBetResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Fcm { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public double BetAmount { get; set; }
    public double OriginValue { get; set; }
    public int BetZoneId { get; set; }
    public string Currency { get; set; } = "EUR";
}

public class CreateBetResult
{
    public int BetId { get; set; }
    public double RemainingPoints { get; set; }
}
