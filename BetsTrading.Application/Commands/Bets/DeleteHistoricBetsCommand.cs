using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteHistoricBetsCommand : IRequest<int>
{
    public string UserId { get; set; } = string.Empty;
}
