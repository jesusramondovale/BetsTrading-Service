using MediatR;

namespace BetsTrading.Application.Commands.DailyReward;

public class ClaimDailyRewardCommand : IRequest<ClaimDailyRewardResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class ClaimDailyRewardResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int CoinsAwarded { get; set; }
    public int NewStreakDay { get; set; }
}
