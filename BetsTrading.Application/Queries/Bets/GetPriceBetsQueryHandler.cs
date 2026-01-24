using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public class GetPriceBetsQueryHandler : IRequestHandler<GetPriceBetsQuery, IEnumerable<PriceBetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPriceBetsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PriceBetDto>> Handle(GetPriceBetsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<PriceBetDto> priceBetDtos;

        if (request.Currency == "EUR")
        {
            var priceBets = await _unitOfWork.PriceBets.GetUserPriceBetsAsync(request.UserId, includeArchived: false, cancellationToken);
            priceBetDtos = new List<PriceBetDto>();

            foreach (var priceBet in priceBets)
            {
                var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(priceBet.Ticker, cancellationToken);
                if (asset == null) continue;

                ((List<PriceBetDto>)priceBetDtos).Add(new PriceBetDto
                {
                    Id = priceBet.Id,
                    Name = asset.Name,
                    Ticker = priceBet.Ticker,
                    PriceBet = priceBet.PriceBetValue,
                    Paid = priceBet.Paid,
                    Prize = priceBet.Prize,
                    Margin = priceBet.Margin,
                    UserId = priceBet.UserId,
                    BetDate = priceBet.BetDate,
                    EndDate = priceBet.EndDate,
                    IconPath = asset.Icon ?? "null",
                    Archived = priceBet.Archived
                });
            }
        }
        else
        {
            var priceBets = await _unitOfWork.PriceBetsUSD.GetUserPriceBetsAsync(request.UserId, includeArchived: false, cancellationToken);
            priceBetDtos = new List<PriceBetDto>();

            foreach (var priceBet in priceBets)
            {
                var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(priceBet.Ticker, cancellationToken);
                if (asset == null) continue;

                ((List<PriceBetDto>)priceBetDtos).Add(new PriceBetDto
                {
                    Id = priceBet.Id,
                    Name = asset.Name,
                    Ticker = priceBet.Ticker,
                    PriceBet = priceBet.PriceBetValue,
                    Paid = priceBet.Paid,
                    Prize = priceBet.Prize,
                    Margin = priceBet.Margin,
                    UserId = priceBet.UserId,
                    BetDate = priceBet.BetDate,
                    EndDate = priceBet.EndDate,
                    IconPath = asset.Icon ?? "null",
                    Archived = priceBet.Archived
                });
            }
        }

        return priceBetDtos;
    }
}
