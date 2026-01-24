using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public class GetHistoricPriceBetsQueryHandler : IRequestHandler<GetHistoricPriceBetsQuery, IEnumerable<PriceBetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetHistoricPriceBetsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PriceBetDto>> Handle(GetHistoricPriceBetsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<PriceBetDto> priceBetDtos;

        if (request.Currency == "EUR")
        {
            var allPriceBets = await _unitOfWork.PriceBets.GetUserPriceBetsAsync(request.UserId, includeArchived: true, cancellationToken);
            var archivedPriceBets = allPriceBets.Where(pb => pb.Archived).ToList();
            priceBetDtos = new List<PriceBetDto>();

            foreach (var priceBet in archivedPriceBets)
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
            var allPriceBets = await _unitOfWork.PriceBetsUSD.GetUserPriceBetsAsync(request.UserId, includeArchived: true, cancellationToken);
            var archivedPriceBets = allPriceBets.Where(pb => pb.Archived).ToList();
            priceBetDtos = new List<PriceBetDto>();

            foreach (var priceBet in archivedPriceBets)
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
