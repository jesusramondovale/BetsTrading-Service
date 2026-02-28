namespace BetsTrading.Domain.Entities;

/// <summary>
/// Tracks daily login reward streak per user.
/// streak_day: last completed day (1-6). 0 = never claimed or streak reset.
/// last_claimed_at: UTC time of last claim; used for 24h/48h window.
/// </summary>
public class DailyLoginStreak
{
    private DailyLoginStreak() { }

    public DailyLoginStreak(string userId, DateTime? lastClaimedAt, int streakDay)
    {
        UserId = userId;
        LastClaimedAt = lastClaimedAt;
        StreakDay = Math.Clamp(streakDay, 0, 6);
    }

    public string UserId { get; private set; } = string.Empty;
    public DateTime? LastClaimedAt { get; private set; }
    public int StreakDay { get; private set; }

    public void RecordClaim(int dayClaimed, DateTime claimedAtUtc)
    {
        StreakDay = Math.Clamp(dayClaimed, 1, 6);
        LastClaimedAt = claimedAtUtc;
    }

    public void ResetStreak()
    {
        StreakDay = 0;
        LastClaimedAt = null;
    }
}
