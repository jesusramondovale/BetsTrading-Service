using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Application.Services;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public class GetHistoricUserBetsQueryHandler : IRequestHandler<GetHistoricUserBetsQuery, IEnumerable<BetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetHistoricUserBetsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<BetDto>> Handle(GetHistoricUserBetsQuery request, CancellationToken cancellationToken)
    {
        var bets = await _unitOfWork.Bets.GetUserBetsAsync(request.UserId, includeArchived: true, cancellationToken);
        var archivedBets = bets.Where(b => b.Archived).ToList();

        var betDtos = new List<BetDto>();

        foreach (var bet in archivedBets)
        {
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(bet.Ticker, cancellationToken);
            if (asset == null) continue;

            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone != null)
            {
                double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZone, "EUR");
                TimeSpan timeMargin = betZone.EndDate - betZone.StartDate;
                var profitLoss = bet.TargetWon ? bet.BetAmount * bet.OriginOdds : -bet.BetAmount;
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
                    Archived = bet.Archived,
                    ProfitLoss = profitLoss
                });
                continue;
            }

            var betZoneUsd = await _unitOfWork.BetZonesUSD.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZoneUsd == null) continue;

            double necessaryGainUsd = BetCalculationService.CalculateNecessaryGain(asset, betZoneUsd, "USD");
            TimeSpan timeMarginUsd = betZoneUsd.EndDate - betZoneUsd.StartDate;
            var profitLossUsd = bet.TargetWon ? bet.BetAmount * bet.OriginOdds : -bet.BetAmount;
            betDtos.Add(new BetDto
            {
                Id = bet.Id,
                UserId = bet.UserId,
                Ticker = bet.Ticker,
                Name = asset.Name,
                BetAmount = bet.BetAmount,
                NecessaryGain = necessaryGainUsd,
                OriginValue = bet.OriginValue,
                CurrentValue = asset.CurrentUsd,
                TargetValue = betZoneUsd.TargetValue,
                TargetMargin = betZoneUsd.BetMargin,
                TargetDate = betZoneUsd.StartDate,
                EndDate = betZoneUsd.EndDate,
                TargetOdds = bet.OriginOdds,
                TargetWon = bet.TargetWon,
                Finished = bet.Finished,
                IconPath = asset.Icon ?? "null",
                Type = betZoneUsd.BetType,
                DateMargin = timeMarginUsd.Days,
                BetZone = bet.BetZoneId,
                Archived = bet.Archived,
                ProfitLoss = profitLossUsd
            });
        }

        return betDtos
            .OrderByDescending(b => b.EndDate ?? b.TargetDate ?? DateTime.MinValue)
            .ThenByDescending(b => b.Id);
    }
}
