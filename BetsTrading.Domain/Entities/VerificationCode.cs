namespace BetsTrading.Domain.Entities;

public class VerificationCode
{
    private VerificationCode() { }

    public VerificationCode(string email, string code, DateTime createdAt, DateTime expiresAt)
    {
        Email = email;
        Code = code;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Verified = false;
    }

    public int Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool Verified { get; private set; }

    public void MarkAsVerified()
    {
        Verified = true;
    }

    public bool IsExpired()
    {
        return DateTime.UtcNow > ExpiresAt;
    }
}
