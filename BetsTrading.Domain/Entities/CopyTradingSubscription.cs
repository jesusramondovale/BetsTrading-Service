namespace BetsTrading.Domain.Entities;

public class CopyTradingSubscription
{
    private CopyTradingSubscription() { }

    public CopyTradingSubscription(
        string followerUserId,
        string targetUserId,
        double copyPercent,
        bool autoAdjustByBalance,
        bool stopAfterOneLoss)
    {
        Id = Guid.NewGuid();
        FollowerUserId = followerUserId;
        TargetUserId = targetUserId;
        CopyPercent = copyPercent;
        AutoAdjustByBalance = autoAdjustByBalance;
        StopAfterOneLoss = stopAfterOneLoss;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string FollowerUserId { get; private set; } = string.Empty;
    public string TargetUserId { get; private set; } = string.Empty;
    public double CopyPercent { get; private set; }
    public bool AutoAdjustByBalance { get; private set; }
    public bool StopAfterOneLoss { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastCopiedAt { get; private set; }
    public DateTime? StoppedAt { get; private set; }
    public string? StopReason { get; private set; }

    public void UpdateSettings(double copyPercent, bool autoAdjustByBalance, bool stopAfterOneLoss)
    {
        CopyPercent = copyPercent;
        AutoAdjustByBalance = autoAdjustByBalance;
        StopAfterOneLoss = stopAfterOneLoss;
        IsActive = true;
        StoppedAt = null;
        StopReason = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCopiedNow()
    {
        LastCopiedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Stop(string reason)
    {
        IsActive = false;
        StopReason = reason;
        StoppedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
