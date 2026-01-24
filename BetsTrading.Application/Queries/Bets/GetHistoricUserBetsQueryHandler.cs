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
        // Obtener apuestas archivadas (históricas)
        var bets = await _unitOfWork.Bets.GetUserBetsAsync(request.UserId, includeArchived: true, cancellationToken);
        var archivedBets = bets.Where(b => b.Archived).ToList();

        var betDtos = new List<BetDto>();

        foreach (var bet in archivedBets)
        {
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(bet.Ticker, cancellationToken);
            if (asset == null) continue;

            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone == null) continue;

            // Calcular ganancia necesaria
            double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZone, "EUR");

            // Calcular margen de tiempo
            TimeSpan timeMargin = betZone.EndDate - betZone.StartDate;

            var betDto = new BetDto
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
            };

            betDtos.Add(betDto);
        }

        return betDtos;
    }
}
