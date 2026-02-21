using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.DTOs;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Queries.Info;

/// <summary>
/// Devuelve los 5 tickers con mayor max odd para la moneda del usuario (EUR/USD),
/// calculados en tiempo real desde el servicio en memoria. Ya no usa la tabla Trends.
/// </summary>
public class GetTrendsQueryHandler : IRequestHandler<GetTrendsQuery, GetTrendsResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITickerMaxOddsService _tickerMaxOddsService;

    public GetTrendsQueryHandler(IUnitOfWork unitOfWork, ITickerMaxOddsService tickerMaxOddsService)
    {
        _unitOfWork = unitOfWork;
        _tickerMaxOddsService = tickerMaxOddsService;
    }

    public async Task<GetTrendsResult> Handle(GetTrendsQuery request, CancellationToken cancellationToken)
    {
        var currency = string.Equals(request.Currency, "USD", StringComparison.OrdinalIgnoreCase) ? "USD" : "EUR";
        var top5 = _tickerMaxOddsService.GetTopTickersByMaxOdd(currency, 5);

        if (top5.Count == 0)
        {
            return new GetTrendsResult
            {
                Success = true,
                Trends = new List<TrendDto>()
            };
        }

        var trendDtos = new List<TrendDto>();
        for (int i = 0; i < top5.Count; i++)
        {
            var (ticker, maxOdd, direction, zoneId, timeframe) = top5[i];
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(ticker, cancellationToken);
            if (asset == null) continue;

            var currentPrice = currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
            var (dailyGain, prevClose) = await GetDailyGainAsync(asset, currency, cancellationToken);

            trendDtos.Add(new TrendDto
            {
                Id = i + 1,
                Name = asset.Name,
                Icon = asset.Icon ?? "null",
                DailyGain = dailyGain,
                Close = prevClose,
                Current = currentPrice,
                Ticker = ticker,
                CurrentMaxOdd = maxOdd,
                CurrentMaxOddDirection = direction,
                CurrentMaxOddZoneId = zoneId,
                CurrentMaxOddTimeframe = timeframe
            });
        }

        return new GetTrendsResult
        {
            Success = true,
            Trends = trendDtos
        };
    }

    private async Task<(double DailyGain, double PrevClose)> GetDailyGainAsync(
        FinancialAsset asset,
        string currency,
        CancellationToken cancellationToken)
    {
        var lastCandle = await _unitOfWork.AssetCandles
            .GetLatestCandleAsync(asset.Id, "1h", cancellationToken);
        if (lastCandle == null)
        {
            var current = currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
            var prev = current * 0.95;
            var gain = prev == 0 ? 0 : ((current - prev) / prev) * 100.0;
            return (gain, prev);
        }

        var lastDay = lastCandle.DateTime.Date;
        AssetCandle? prevCandle;

        if (asset.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase) ||
            asset.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
        {
            var candles = (await _unitOfWork.AssetCandles
                    .GetCandlesByAssetAsync(asset.Id, "1h", 25, cancellationToken))
                .OrderByDescending(c => c.DateTime)
                .ToList();
            prevCandle = candles.Count > 24 ? candles[24] : null;
        }
        else
        {
            var candles = await _unitOfWork.AssetCandles
                .GetCandlesByAssetAsync(asset.Id, "1h", 100, cancellationToken);
            prevCandle = candles
                .Where(c => c.DateTime.Date < lastDay)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefault();
        }

        var currentPrice = currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
        double closeVal;
        double gainVal;

        if (prevCandle != null)
        {
            closeVal = (double)prevCandle.Close;
            gainVal = closeVal == 0 ? 0 : ((currentPrice - closeVal) / closeVal) * 100.0;
        }
        else
        {
            closeVal = currentPrice * 0.95;
            gainVal = ((currentPrice - closeVal) / closeVal) * 100.0;
        }

        return (gainVal, closeVal);
    }
}
