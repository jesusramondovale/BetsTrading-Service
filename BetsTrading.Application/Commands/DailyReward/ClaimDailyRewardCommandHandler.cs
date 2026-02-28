using MediatR;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Queries.DailyReward;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.DailyReward;

public class ClaimDailyRewardCommandHandler : IRequestHandler<ClaimDailyRewardCommand, ClaimDailyRewardResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public ClaimDailyRewardCommandHandler(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ClaimDailyRewardResult> Handle(ClaimDailyRewardCommand request, CancellationToken cancellationToken)
    {
        _logger.Debug("[DAILY_REWARD HANDLER] ClaimDailyReward Handle START UserId={0}", request.UserId ?? "null");
        var now = DateTime.UtcNow;
        var streak = await _unitOfWork.DailyLoginStreaks.GetByUserIdAsync(request.UserId, cancellationToken);
        _logger.Debug("[DAILY_REWARD HANDLER] GetByUserIdAsync streak={0} (null? {1} StreakDay={2} LastClaimedAt={3})",
            streak != null ? "found" : "null", streak == null, streak?.StreakDay ?? -1, streak?.LastClaimedAt?.ToString("O") ?? "null");

        int dayToClaim;
        var isNewStreak = false;
        if (streak == null || (streak.StreakDay == 0 && streak.LastClaimedAt == null))
        {
            _logger.Debug("[DAILY_REWARD HANDLER] Branch: first time -> dayToClaim=1, creating new DailyLoginStreak");
            dayToClaim = 1;
            streak = new DailyLoginStreak(request.UserId, null, 0);
            await _unitOfWork.DailyLoginStreaks.AddAsync(streak, cancellationToken);
            isNewStreak = true;
            _logger.Debug("[DAILY_REWARD HANDLER] AddAsync(new streak) done");
        }
        else
        {
            var elapsed = now - (streak.LastClaimedAt ?? now);
            _logger.Debug("[DAILY_REWARD HANDLER] elapsed since last claim = {0} (24h={1} 48h={2})", elapsed.TotalHours, DailyRewardConstants.WindowToClaim.TotalHours, DailyRewardConstants.WindowUntilStreakLost.TotalHours);
            if (elapsed >= DailyRewardConstants.WindowUntilStreakLost)
            {
                _logger.Debug("[DAILY_REWARD HANDLER] Branch: streak lost -> ResetStreak dayToClaim=1");
                streak.ResetStreak();
                _unitOfWork.DailyLoginStreaks.Update(streak);
                dayToClaim = 1;
            }
            else if (elapsed < DailyRewardConstants.WindowToClaim)
            {
                _logger.Debug("[DAILY_REWARD HANDLER] Branch: too early -> return Success=false");
                return new ClaimDailyRewardResult
                {
                    Success = false,
                    Message = "Reward not yet available. Wait until 24h after last claim.",
                };
            }
            else
            {
                dayToClaim = streak.StreakDay + 1;
                if (dayToClaim > 6) dayToClaim = 1;
                _logger.Debug("[DAILY_REWARD HANDLER] Branch: next day -> dayToClaim={0}", dayToClaim);
            }
        }

        var coins = DailyRewardConstants.CoinsByDay[dayToClaim - 1];
        _logger.Debug("[DAILY_REWARD HANDLER] coins for day {0} = {1}", dayToClaim, coins);
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            _logger.Debug("[DAILY_REWARD HANDLER] User not found UserId={0}", request.UserId);
            return new ClaimDailyRewardResult { Success = false, Message = "User not found." };
        }
        _logger.Debug("[DAILY_REWARD HANDLER] user found Points before AddPoints={0}", user.Points);

        user.AddPoints(coins);
        streak.RecordClaim(dayToClaim, now);
        _unitOfWork.Users.Update(user);
        if (!isNewStreak)
            _unitOfWork.DailyLoginStreaks.Update(streak);
        _logger.Debug("[DAILY_REWARD HANDLER] AddPoints({0}) RecordClaim({1}) Update(user) Update(streak)={2} -> calling SaveChangesAsync", coins, dayToClaim, !isNewStreak);
        var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.Debug("[DAILY_REWARD HANDLER] SaveChangesAsync returned {0} (rows affected)", saved);

        return new ClaimDailyRewardResult
        {
            Success = true,
            CoinsAwarded = coins,
            NewStreakDay = dayToClaim,
            Message = "Daily reward claimed.",
        };
    }
}
