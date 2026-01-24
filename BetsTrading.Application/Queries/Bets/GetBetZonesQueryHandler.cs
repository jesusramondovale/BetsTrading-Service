using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;
using AutoMapper;

namespace BetsTrading.Application.Queries.Bets;

public class GetBetZonesQueryHandler : IRequestHandler<GetBetZonesQuery, GetBetZonesResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GetBetZonesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<GetBetZonesResult> Handle(GetBetZonesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Currency == "EUR")
            {
                var betZones = await _unitOfWork.BetZones.GetActiveBetZonesByTickerAsync(
                    request.Ticker, request.Timeframe, cancellationToken);

                if (!betZones.Any())
                {
                    return new GetBetZonesResult
                    {
                        Success = false,
                        Message = "No bets found for this ticker"
                    };
                }

                var betZoneDtos = _mapper.Map<List<BetZoneDto>>(betZones);
                return new GetBetZonesResult
                {
                    Success = true,
                    BetZones = betZoneDtos
                };
            }
            else
            {
                var betZonesUSD = await _unitOfWork.BetZonesUSD.GetActiveBetZonesByTickerAsync(
                    request.Ticker, request.Timeframe, cancellationToken);

                if (!betZonesUSD.Any())
                {
                    return new GetBetZonesResult
                    {
                        Success = false,
                        Message = "No bets found for this ticker"
                    };
                }

                var betZoneDtos = _mapper.Map<List<BetZoneDto>>(betZonesUSD);
                return new GetBetZonesResult
                {
                    Success = true,
                    BetZones = betZoneDtos
                };
            }
        }
        catch (Exception ex)
        {
            return new GetBetZonesResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
