using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class CreatePriceBetCommand : IRequest<CreatePriceBetResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Fcm { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public double PriceBet { get; set; }
    public double Margin { get; set; }
    public DateTime EndDate { get; set; }
    public string Currency { get; set; } = "EUR";
}

public class CreatePriceBetResult
{
    public int PriceBetId { get; set; }
    public double RemainingPoints { get; set; }
}
