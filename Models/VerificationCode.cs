using System;

namespace BetsTrading_Service.Models
{
  public class VerificationCode
  {
    public VerificationCode() { }

    public VerificationCode(string email, string code, DateTime createdAt, DateTime expiresAt)
    {
      Email = email;
      Code = code;
      CreatedAt = createdAt;
      ExpiresAt = expiresAt;
      Verified = false;
    }

    public int Id { get; private set; }
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool Verified { get; set; }
  }
}
