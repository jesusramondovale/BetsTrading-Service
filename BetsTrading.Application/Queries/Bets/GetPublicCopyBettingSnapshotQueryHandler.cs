using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.Bets;

public sealed class GetPublicCopyBettingSnapshotQueryHandler
    : IRequestHandler<GetPublicCopyBettingSnapshotQuery, PublicCopyBettingSnapshotDto>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;

    public GetPublicCopyBettingSnapshotQueryHandler(IMediator mediator, IUnitOfWork unitOfWork)
    {
        _mediator = mediator;
        _unitOfWork = unitOfWork;
    }

    public async Task<PublicCopyBettingSnapshotDto> Handle(
        GetPublicCopyBettingSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.TargetUserId.Trim();
        if (string.IsNullOrEmpty(userId))
        {
            return new PublicCopyBettingSnapshotDto
            {
                Stats = new CopyBettingStatsDto(),
                RecentBets = new List<CopyBettingRecentItemDto>()
            };
        }

        var bets = (await _mediator.Send(
            new GetHistoricUserBetsQuery { UserId = userId },
            cancellationToken)).ToList();

        var priceBets = (await _mediator.Send(
            new GetHistoricPriceBetsQuery { UserId = userId, Currency = "EUR" },
            cancellationToken)).ToList();

        var finished = bets.Count;
        var totalStaked = finished > 0 ? bets.Sum(b => b.BetAmount) : 0d;
        var totalPl = finished > 0 ? bets.Sum(b => b.ProfitLoss) : 0d;
        var wins = bets.Count(b => b.TargetWon);
        var losses = finished - wins;
        double? winRate = finished > 0 ? 100.0 * wins / finished : null;

        var stats = new CopyBettingStatsDto
        {
            FinishedBets = finished,
            TotalStakedCoins = totalStaked,
            TotalProfitLossCoins = totalPl,
            Wins = wins,
            Losses = losses,
            WinRatePercent = winRate
        };

        var merged = new List<(DateTime SortUtc, CopyBettingRecentItemDto Item)>();

        foreach (var b in bets)
        {
            var end = b.EndDate ?? b.TargetDate ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            var utc = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();
            merged.Add((utc, new CopyBettingRecentItemDto { Kind = "bet", Bet = b }));
        }

        foreach (var p in priceBets)
        {
            var utc = p.EndDate.Kind == DateTimeKind.Utc
                ? p.EndDate
                : p.EndDate.ToUniversalTime();
            merged.Add((utc, new CopyBettingRecentItemDto { Kind = "priceBet", PriceBet = p }));
        }

        var recent = merged
            .OrderByDescending(x => x.SortUtc)
            .Take(15)
            .Select(x => x.Item)
            .ToList();

        ViewerCopyTradingStatusDto? viewerStatus = null;
        var viewerId = (request.ViewerUserId ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(viewerId) && viewerId != userId)
        {
            var subscription = await _unitOfWork.CopyTradingSubscriptions
                .GetByFollowerAndTargetAsync(viewerId, userId, cancellationToken);

            if (subscription != null && subscription.IsActive)
            {
                viewerStatus = new ViewerCopyTradingStatusDto
                {
                    IsActive = true,
                    CopyPercent = subscription.CopyPercent,
                    AutoAdjustByBalance = subscription.AutoAdjustByBalance,
                    StopAfterOneLoss = subscription.StopAfterOneLoss
                };
            }
            else
            {
                viewerStatus = new ViewerCopyTradingStatusDto { IsActive = false };
            }
        }

        return new PublicCopyBettingSnapshotDto
        {
            Stats = stats,
            RecentBets = recent,
            ViewerCopyTrading = viewerStatus
        };
    }
}
