using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public class GetBetZoneQueryHandler : IRequestHandler<GetBetZoneQuery, BetZoneDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBetZoneQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BetZoneDto?> Handle(GetBetZoneQuery request, CancellationToken cancellationToken)
    {
        // Obtener la apuesta para encontrar su bet zone
        var bet = await _unitOfWork.Bets.GetByIdAsync(request.BetId, cancellationToken);
        if (bet == null)
            return null;

        double originOdds = bet.OriginOdds;

        if (request.Currency == "EUR")
        {
            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone == null)
                return null;

            return new BetZoneDto
            {
                Id = betZone.Id,
                Ticker = betZone.Ticker,
                TargetValue = betZone.TargetValue,
                BetMargin = betZone.BetMargin,
                StartDate = betZone.StartDate,
                EndDate = betZone.EndDate,
                TargetOdds = originOdds,
                BetType = betZone.BetType,
                Timeframe = betZone.Timeframe
            };
        }
        else
        {
            var betZoneUSD = await _unitOfWork.BetZonesUSD.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZoneUSD == null)
                return null;

            return new BetZoneDto
            {
                Id = betZoneUSD.Id,
                Ticker = betZoneUSD.Ticker,
                TargetValue = betZoneUSD.TargetValue,
                BetMargin = betZoneUSD.BetMargin,
                StartDate = betZoneUSD.StartDate,
                EndDate = betZoneUSD.EndDate,
                TargetOdds = originOdds,
                BetType = betZoneUSD.BetType,
                Timeframe = betZoneUSD.Timeframe
            };
        }
    }
}
