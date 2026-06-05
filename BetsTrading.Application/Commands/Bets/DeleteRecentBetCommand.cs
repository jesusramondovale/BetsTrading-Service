using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteRecentBetCommand : IRequest<bool>
{
    public string UserId { get; set; } = string.Empty;
    public int BetId { get; set; }
}
