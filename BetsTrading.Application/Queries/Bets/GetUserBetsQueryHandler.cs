using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Application.Services;
using BetsTrading.Domain.Interfaces;
using AutoMapper;

namespace BetsTrading.Application.Queries.Bets;

public class GetUserBetsQueryHandler : IRequestHandler<GetUserBetsQuery, IEnumerable<BetDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetUserBetsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<BetDto>> Handle(GetUserBetsQuery request, CancellationToken cancellationToken)
    {
        var bets = await _unitOfWork.Bets.GetUserBetsAsync(request.UserId, request.IncludeArchived, cancellationToken);
        
        var betDtos = new List<BetDto>();

        foreach (var bet in bets)
        {
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(bet.Ticker, cancellationToken);
            if (asset == null) continue;

            // La bet puede referenciar BetZones (EUR) o BetZonesUSD (USD). Probar ambos.
            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone != null)
            {
                double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZone, "EUR");
                TimeSpan timeMargin = betZone.EndDate - betZone.StartDate;
                betDtos.Add(new BetDto
                {
                    Id = bet.Id,
                    UserId = bet.UserId,
                    Ticker = bet.Ticker,
                    Name = asset.Name,
                    BetAmount = bet.BetAmount,
                    NecessaryGain = necessaryGain,
                    OriginValue = bet.OriginValue,
                    CurrentValue = asset.CurrentEur,
                    TargetValue = betZone.TargetValue,
                    TargetMargin = betZone.BetMargin,
                    TargetDate = betZone.StartDate,
                    EndDate = betZone.EndDate,
                    TargetOdds = bet.OriginOdds,
                    TargetWon = bet.TargetWon,
                    Finished = bet.Finished,
                    IconPath = asset.Icon ?? "null",
                    Type = betZone.BetType,
                    DateMargin = timeMargin.Days,
                    BetZone = bet.BetZoneId,
                    Archived = bet.Archived
                });
            }
            else
            {
                var betZoneUSD = await _unitOfWork.BetZonesUSD.GetByIdAsync(bet.BetZoneId, cancellationToken);
                if (betZoneUSD == null) continue;

                double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZoneUSD, "USD");
                TimeSpan timeMargin = betZoneUSD.EndDate - betZoneUSD.StartDate;
                betDtos.Add(new BetDto
                {
                    Id = bet.Id,
                    UserId = bet.UserId,
                    Ticker = bet.Ticker,
                    Name = asset.Name,
                    BetAmount = bet.BetAmount,
                    NecessaryGain = necessaryGain,
                    OriginValue = bet.OriginValue,
                    CurrentValue = asset.CurrentUsd,
                    TargetValue = betZoneUSD.TargetValue,
                    TargetMargin = betZoneUSD.BetMargin,
                    TargetDate = betZoneUSD.StartDate,
                    EndDate = betZoneUSD.EndDate,
                    TargetOdds = bet.OriginOdds,
                    TargetWon = bet.TargetWon,
                    Finished = bet.Finished,
                    IconPath = asset.Icon ?? "null",
                    Type = betZoneUSD.BetType,
                    DateMargin = timeMargin.Days,
                    BetZone = bet.BetZoneId,
                    Archived = bet.Archived
                });
            }
            // Si no existe en ninguna tabla (datos inconsistentes), se omite la bet
        }

        return betDtos;
    }
}
