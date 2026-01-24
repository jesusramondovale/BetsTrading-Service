using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Application.Services;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public class GetUserBetQueryHandler : IRequestHandler<GetUserBetQuery, BetDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserBetQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BetDto?> Handle(GetUserBetQuery request, CancellationToken cancellationToken)
    {
        // Obtener la apuesta específica del usuario
        var bet = await _unitOfWork.Bets.GetByIdAsync(request.BetId, cancellationToken);
        if (bet == null || bet.UserId != request.UserId)
            return null;

        var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(bet.Ticker, cancellationToken);
        if (asset == null)
            return null;

        double currentValue = request.Currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;

        if (request.Currency == "EUR")
        {
            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone == null)
                return null;

            // Calcular ganancia necesaria
            double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZone, request.Currency);

            // Calcular margen de tiempo
            TimeSpan timeMargin = betZone.EndDate - betZone.StartDate;

            return new BetDto
            {
                Id = bet.Id,
                UserId = bet.UserId,
                Ticker = bet.Ticker,
                Name = asset.Name,
                BetAmount = bet.BetAmount,
                NecessaryGain = necessaryGain,
                OriginValue = bet.OriginValue,
                CurrentValue = currentValue,
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
        }
        else
        {
            var betZoneUSD = await _unitOfWork.BetZonesUSD.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZoneUSD == null)
                return null;

            // Calcular ganancia necesaria para USD
            double necessaryGain = BetCalculationService.CalculateNecessaryGain(asset, betZoneUSD, request.Currency);

            // Calcular margen de tiempo
            TimeSpan timeMargin = betZoneUSD.EndDate - betZoneUSD.StartDate;

            return new BetDto
            {
                Id = bet.Id,
                UserId = bet.UserId,
                Ticker = bet.Ticker,
                Name = asset.Name,
                BetAmount = bet.BetAmount,
                NecessaryGain = necessaryGain,
                OriginValue = bet.OriginValue,
                CurrentValue = currentValue,
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
            };
        }
    }
}
