using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteRecentPriceBetCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public int PriceBetId { get; set; }
    public string Currency { get; set; } = "EUR";
}
