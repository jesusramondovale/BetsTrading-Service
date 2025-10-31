namespace BetsTrading_Service.Models
{
  public class VerificationCode
  {
    public VerificationCode() { }

    public VerificationCode(string anEmail, string aCode, DateTime aCreatedAt, DateTime anExpiresAt)
    {
      email = anEmail;
      code = aCode;
      createdAt = aCreatedAt;
      expiresAt = anExpiresAt;
      verified = false;
    }

    public int id { get; private set; }
    public string email { get; set; } = string.Empty;
    public string code { get; set; } = string.Empty;
    public DateTime createdAt { get; private set; }
    public DateTime expiresAt { get; private set; }
    public bool verified { get; set; }
  }
}
