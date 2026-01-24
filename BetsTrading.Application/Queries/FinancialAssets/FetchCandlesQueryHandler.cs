using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.FinancialAssets;

public class FetchCandlesQueryHandler : IRequestHandler<FetchCandlesQuery, FetchCandlesResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public FetchCandlesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>Límite alto de velas para alinear con legacy FetchCandles (sin límite). ~5.7 años de 1h.</summary>
    private const int MaxCandles = 50_000;

    public async Task<FetchCandlesResult> Handle(FetchCandlesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var ticker = request.Ticker ?? request.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ticker))
            {
                return new FetchCandlesResult
                {
                    Success = false,
                    Message = "Ticker or id is required"
                };
            }

            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(ticker.Trim(), cancellationToken);
            if (asset == null)
            {
                return new FetchCandlesResult
                {
                    Success = false,
                    Message = "Asset not found"
                };
            }

            var timeframe = request.Timeframe ?? 1;
            var currency = request.Currency ?? "EUR";

            // Comportamiento legacy: mismas velas para EUR y USD (solo cambia AssetCandles vs AssetCandlesUSD),
            // todas las velas, OrderByDescending(DateTime). Sin límite de 30 días ni de 1000.
            IEnumerable<CandleDto> rawDtos;
            if (currency == "EUR")
            {
                var candles = await _unitOfWork.AssetCandles.GetCandlesByAssetAsync(asset.Id, "1h", MaxCandles, cancellationToken);
                rawDtos = candles.Select(c => new CandleDto
                {
                    DateTime = c.DateTime,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close
                });
            }
            else
            {
                var candles = await _unitOfWork.AssetCandlesUSD.GetCandlesByAssetAsync(asset.Id, "1h", MaxCandles, cancellationToken);
                rawDtos = candles.Select(c => new CandleDto
                {
                    DateTime = c.DateTime,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close
                });
            }

            var list = rawDtos.ToList();
            if (!list.Any())
            {
                return new FetchCandlesResult
                {
                    Success = true,
                    Candles = new List<CandleDto>()
                };
            }

            var candleDtos = ProcessCandles(list, timeframe);
            return new FetchCandlesResult
            {
                Success = true,
                Candles = candleDtos
            };
        }
        catch (Exception ex)
        {
            return new FetchCandlesResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private List<CandleDto> ProcessCandles(IEnumerable<CandleDto> candles, int timeframe)
    {
        if (timeframe <= 1)
        {
            return candles.ToList();
        }

        // Group candles by timeframe
        return candles
            .GroupBy(c => new DateTime(
                c.DateTime.Year,
                c.DateTime.Month,
                c.DateTime.Day,
                c.DateTime.Hour / timeframe * timeframe,
                0, 0, DateTimeKind.Utc))
            .Select(g => new CandleDto
            {
                DateTime = g.Key,
                Open = g.OrderBy(c => c.DateTime).First().Open,
                Close = g.OrderBy(c => c.DateTime).Last().Close,
                High = g.Max(c => c.High),
                Low = g.Min(c => c.Low)
            })
            .OrderByDescending(c => c.DateTime)
            .ToList();
    }
}
