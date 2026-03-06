using MediatR;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Queries.DailyReward;

public static class DailyRewardConstants
{
    public static readonly int[] CoinsByDay = { 5, 10, 15, 25, 40, 50 };
    public static readonly TimeSpan WindowToClaim = TimeSpan.FromHours(24);
    public static readonly TimeSpan WindowUntilStreakLost = TimeSpan.FromHours(48);
}

public class GetDailyRewardStatusQueryHandler : IRequestHandler<GetDailyRewardStatusQuery, GetDailyRewardStatusResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;
    private readonly IAdminRuntimeConfig _adminConfig;

    public GetDailyRewardStatusQueryHandler(IUnitOfWork unitOfWork, IApplicationLogger logger, IAdminRuntimeConfig adminConfig)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _adminConfig = adminConfig;
    }

    private int[] CoinsByDay => _adminConfig.DailyRewardCoinsByDay ?? DailyRewardConstants.CoinsByDay;
    private TimeSpan WindowToClaim => _adminConfig.DailyRewardWindowToClaimHours.HasValue ? TimeSpan.FromHours(_adminConfig.DailyRewardWindowToClaimHours.Value) : DailyRewardConstants.WindowToClaim;
    private TimeSpan WindowUntilStreakLost => _adminConfig.DailyRewardWindowUntilStreakLostHours.HasValue ? TimeSpan.FromHours(_adminConfig.DailyRewardWindowUntilStreakLostHours.Value) : DailyRewardConstants.WindowUntilStreakLost;

    public async Task<GetDailyRewardStatusResult> Handle(GetDailyRewardStatusQuery request, CancellationToken cancellationToken)
    {
        _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus Handle START UserId={0}", request.UserId ?? "null");
        var now = DateTime.UtcNow;
        var streak = await _unitOfWork.DailyLoginStreaks.GetByUserIdAsync(request.UserId, cancellationToken);
        _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus streak={0} StreakDay={1} LastClaimedAt={2}", streak != null ? "found" : "null", streak?.StreakDay ?? -1, streak?.LastClaimedAt?.ToString("O") ?? "null");

        // First time: no record or never claimed (streak_day 0 and no last claim)
        if (streak == null || (streak.StreakDay == 0 && streak.LastClaimedAt == null))
        {
            _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus RETURN first time ShowDialog=true CanClaim=true CurrentDay=1");
            return new GetDailyRewardStatusResult
            {
                ShowDialog = true,
                CurrentDay = 1,
                CanClaim = true,
                CoinsForCurrentDay = CoinsByDay[0],
                RewardsByDay = CoinsByDay,
            };
        }

        var lastClaimed = streak.LastClaimedAt ?? now;
        var elapsed = now - lastClaimed;
        _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus elapsed={0}h (24h={1} 48h={2})", elapsed.TotalHours, WindowToClaim.TotalHours, WindowUntilStreakLost.TotalHours);

        // Streak lost: more than 48h since last claim
        if (elapsed >= WindowUntilStreakLost)
        {
            _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus RETURN streak lost ShowDialog=true CanClaim=true CurrentDay=1");
            return new GetDailyRewardStatusResult
            {
                ShowDialog = true,
                CurrentDay = 1,
                CanClaim = true,
                CoinsForCurrentDay = CoinsByDay[0],
                RewardsByDay = CoinsByDay,
            };
        }

        // Within 24h of last claim: next reward not yet available
        if (elapsed < WindowToClaim)
        {
            var nextAvailable = lastClaimed.Add(WindowToClaim);
            var currentDayDisplay = streak.StreakDay + 1; // next day to claim (1-6)
            if (currentDayDisplay > 6) currentDayDisplay = 1;
            var coinsNext = CoinsByDay[Math.Min(currentDayDisplay - 1, CoinsByDay.Length - 1)];
            _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus RETURN within 24h ShowDialog=false CanClaim=false CurrentDay={0}", currentDayDisplay);
            return new GetDailyRewardStatusResult
            {
                ShowDialog = false,
                CurrentDay = currentDayDisplay,
                CanClaim = false,
                CoinsForCurrentDay = coinsNext,
                NextAvailableAtUtc = nextAvailable,
                RewardsByDay = CoinsByDay,
            };
        }

        // Between 24h and 48h: can claim next day
        var nextDay = streak.StreakDay + 1;
        if (nextDay > 6) nextDay = 1;
        var coins = CoinsByDay[Math.Min(nextDay - 1, CoinsByDay.Length - 1)];
        _logger.Debug("[DAILY_REWARD HANDLER] GetDailyRewardStatus RETURN can claim next day ShowDialog=true CanClaim=true CurrentDay={0} Coins={1}", nextDay, coins);
        return new GetDailyRewardStatusResult
        {
            ShowDialog = true,
            CurrentDay = nextDay,
            CanClaim = true,
            CoinsForCurrentDay = coins,
            RewardsByDay = CoinsByDay,
        };
    }
}
