namespace BetsTrading.Domain.Entities;

public class RewardNonce
{
    private RewardNonce() { }

    public RewardNonce(string userId, string adUnitId, string? purpose, int? coins)
    {
        Id = Guid.NewGuid();
        Nonce = GenerateBase64UrlNonce(24);
        UserId = userId;
        AdUnitId = adUnitId;
        Purpose = purpose;
        Coins = coins;
        Used = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(5);
    }

    public Guid Id { get; private set; }
    public string Nonce { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string AdUnitId { get; private set; } = string.Empty;
    public string? Purpose { get; private set; }
    public int? Coins { get; private set; }
    public bool Used { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public void MarkAsUsed()
    {
        Used = true;
        UsedAt = DateTime.UtcNow;
    }

    public bool IsExpired() => ExpiresAt < DateTime.UtcNow;

    private static string GenerateBase64UrlNonce(int bytes = 32)
    {
        var buffer = new byte[bytes];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
