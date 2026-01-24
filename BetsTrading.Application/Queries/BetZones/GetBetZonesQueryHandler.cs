using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.BetZones;

public class GetBetZonesQueryHandler : IRequestHandler<GetBetZonesQuery, IEnumerable<BetZoneDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBetZonesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<BetZoneDto>> Handle(GetBetZonesQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<BetsTrading.Domain.Entities.BetZone> betZones;

        if (!string.IsNullOrEmpty(request.Ticker) && request.Timeframe.HasValue)
        {
            betZones = await _unitOfWork.BetZones.GetActiveBetZonesByTickerAsync(
                request.Ticker, 
                request.Timeframe.Value, 
                cancellationToken);
        }
        else if (request.ActiveOnly)
        {
            betZones = await _unitOfWork.BetZones.FindAsync(
                bz => bz.Active, 
                cancellationToken);
        }
        else
        {
            betZones = await _unitOfWork.BetZones.GetAllAsync(cancellationToken);
        }

        return betZones.Select(bz => new BetZoneDto
        {
            Id = bz.Id,
            Ticker = bz.Ticker,
            TargetValue = bz.TargetValue,
            BetMargin = bz.BetMargin,
            StartDate = bz.StartDate,
            EndDate = bz.EndDate,
            TargetOdds = bz.TargetOdds,
            BetType = bz.BetType,
            Active = bz.Active,
            Timeframe = bz.Timeframe
        });
    }
}
