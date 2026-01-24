using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteRecentBetCommand : IRequest<bool>
{
    public int BetId { get; set; }
}
