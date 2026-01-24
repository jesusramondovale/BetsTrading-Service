using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTrendsQueryHandler : IRequestHandler<GetTrendsQuery, GetTrendsResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTrendsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetTrendsResult> Handle(GetTrendsQuery request, CancellationToken cancellationToken)
    {
        var trends = await _unitOfWork.Trends.GetAllAsync(cancellationToken);

        if (!trends.Any())
        {
            return new GetTrendsResult
            {
                Success = false,
                Message = "No trends found"
            };
        }

        var trendDtos = new List<TrendDto>();

        foreach (var trend in trends)
        {
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(trend.Ticker, cancellationToken);
            if (asset == null) continue;

            double prevClose = (request.Currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd) / ((100.0 + trend.DailyGain) / 100.0);

            trendDtos.Add(new TrendDto
            {
                Id = trend.Id,
                Name = asset.Name,
                Icon = asset.Icon ?? "null",
                DailyGain = trend.DailyGain,
                Close = prevClose,
                Current = request.Currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd,
                Ticker = trend.Ticker,
                CurrentMaxOdd = asset.CurrentMaxOdd,
                CurrentMaxOddDirection = asset.CurrentMaxOddDirection
            });
        }

        return new GetTrendsResult
        {
            Success = true,
            Trends = trendDtos
        };
    }
}
