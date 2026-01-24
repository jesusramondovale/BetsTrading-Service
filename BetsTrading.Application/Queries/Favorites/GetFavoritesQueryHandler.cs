using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Entities;

namespace BetsTrading.Application.Queries.Favorites;

public class GetFavoritesQueryHandler : IRequestHandler<GetFavoritesQuery, GetFavoritesResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFavoritesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetFavoritesResult> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await _unitOfWork.Favorites.GetByUserIdAsync(request.UserId, cancellationToken);

        if (!favorites.Any())
        {
            return new GetFavoritesResult { Success = false, Message = "No favorites found" };
        }

        var favoritesDto = new List<FavoriteDto>();

        foreach (var fav in favorites)
        {
            var asset = await _unitOfWork.FinancialAssets.GetByTickerAsync(fav.Ticker, cancellationToken);
            if (asset == null) continue;

            double prevClose;
            double dailyGain;
            double currentClose = request.Currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
            double candleClose;

            if (request.Currency == "EUR")
            {
                var lastCandle = await _unitOfWork.AssetCandles.GetLatestCandleAsync(asset.Id, "1h", cancellationToken);
                if (lastCandle == null) continue;

                var lastDay = lastCandle.DateTime.Date;
                candleClose = (double)lastCandle.Close;

                AssetCandle? prevCandle = null;

                if (asset.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase) || 
                    asset.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
                {
                    var candles = await _unitOfWork.AssetCandles.GetCandlesByAssetAsync(asset.Id, "1h", 25, cancellationToken);
                    prevCandle = candles.Skip(24).FirstOrDefault();
                }
                else
                {
                    var prevDay = lastDay.AddDays(-1);
                    var candles = await _unitOfWork.AssetCandles.GetCandlesByDateRangeAsync(asset.Id, "1h", prevDay, lastDay, cancellationToken);
                    prevCandle = candles.OrderByDescending(c => c.DateTime).FirstOrDefault();
                }

                if (prevCandle != null)
                {
                    prevClose = (double)prevCandle.Close;
                    dailyGain = prevClose == 0 ? 0 : ((currentClose - prevClose) / prevClose) * 100.0;
                }
                else
                {
                    prevClose = asset.CurrentEur * 0.95;
                    dailyGain = ((currentClose - prevClose) / prevClose) * 100.0;
                }
            }
            else
            {
                var lastCandle = await _unitOfWork.AssetCandlesUSD.GetLatestCandleAsync(asset.Id, "1h", cancellationToken);
                if (lastCandle == null) continue;

                var lastDay = lastCandle.DateTime.Date;
                candleClose = (double)lastCandle.Close;

                AssetCandleUSD? prevCandle = null;

                if (asset.Group.Equals("Cryptos", StringComparison.OrdinalIgnoreCase) || 
                    asset.Group.Equals("Forex", StringComparison.OrdinalIgnoreCase))
                {
                    var candles = await _unitOfWork.AssetCandlesUSD.GetCandlesByDateRangeAsync(asset.Id, "1h", lastDay.AddDays(-2), lastDay, cancellationToken);
                    prevCandle = candles.OrderByDescending(c => c.DateTime).Skip(24).FirstOrDefault();
                }
                else
                {
                    var prevDay = lastDay.AddDays(-1);
                    var candles = await _unitOfWork.AssetCandlesUSD.GetCandlesByDateRangeAsync(asset.Id, "1h", prevDay, lastDay, cancellationToken);
                    prevCandle = candles.OrderByDescending(c => c.DateTime).FirstOrDefault();
                }

                if (prevCandle != null)
                {
                    prevClose = (double)prevCandle.Close;
                    dailyGain = prevClose == 0 ? 0 : ((currentClose - prevClose) / prevClose) * 100.0;
                }
                else
                {
                    prevClose = asset.CurrentUsd * 0.95;
                    dailyGain = ((currentClose - prevClose) / prevClose) * 100.0;
                }
            }

            favoritesDto.Add(new FavoriteDto
            {
                Id = fav.Id,
                Name = asset.Name,
                Icon = asset.Icon ?? "noIcon",
                DailyGain = dailyGain,
                Close = prevClose,
                Current = candleClose,
                UserId = request.UserId,
                Ticker = fav.Ticker
            });
        }

        return new GetFavoritesResult
        {
            Success = true,
            Favorites = favoritesDto
        };
    }
}

public class GetFavoritesResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<FavoriteDto> Favorites { get; set; } = new();
}
