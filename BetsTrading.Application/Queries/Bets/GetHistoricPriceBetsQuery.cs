using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public class GetHistoricPriceBetsQuery : IRequest<IEnumerable<PriceBetDto>>
{
    public string UserId { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
}
