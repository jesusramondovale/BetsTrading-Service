using MediatR;

namespace BetsTrading.Application.Queries.DailyReward;

public class GetDailyRewardStatusQuery : IRequest<GetDailyRewardStatusResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetDailyRewardStatusResult
{
    /// <summary>Whether to show the daily reward dialog (user has something to claim or first time).</summary>
    public bool ShowDialog { get; set; }
    /// <summary>Current day in the journey (1-6). Day to claim if CanClaim is true.</summary>
    public int CurrentDay { get; set; }
    /// <summary>User can claim the reward now (must accept dialog).</summary>
    public bool CanClaim { get; set; }
    /// <summary>Coins for the current day (5, 10, 15, 25, 40, 50).</summary>
    public int CoinsForCurrentDay { get; set; }
    /// <summary>UTC when the next reward becomes available (if not yet claimable).</summary>
    public DateTime? NextAvailableAtUtc { get; set; }
    /// <summary>Rewards per day for UI journey: [5, 10, 15, 25, 40, 50].</summary>
    public int[] RewardsByDay { get; set; } = { 5, 10, 15, 25, 40, 50 };
}
