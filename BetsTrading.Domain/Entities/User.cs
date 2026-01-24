namespace BetsTrading.Domain.Entities;

public class User
{
    // Constructor privado para EF Core
    private User() { }

    public User(string id, string fcm, string fullname, string password, string country, 
                string gender, string email, DateTime birthday, string username, 
                string? profilePic = null, double points = 0.0, string? creditCard = null)
    {
        Id = id;
        Fcm = fcm;
        Fullname = fullname;
        Password = password;
        Country = country;
        Gender = gender;
        Email = email;
        Birthday = birthday;
        Username = username;
        ProfilePic = profilePic;
        Points = points;
        CreditCard = creditCard ?? "nullCreditCard";
        SigninDate = DateTime.UtcNow;
        LastSession = DateTime.UtcNow;
        TokenExpiration = DateTime.UtcNow.AddDays(15); // SESSION_EXP_DAYS
        IsVerified = false;
        IsActive = true;
        FailedAttempts = 0;
        PendingBalance = 0.0;
    }

    public string Id { get; private set; } = string.Empty;
    public string Fcm { get; set; } = string.Empty;
    public string Fullname { get; set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Gender { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime Birthday { get; set; }
    public DateTime SigninDate { get; private set; }
    public DateTime LastSession { get; set; }
    public string Username { get; private set; } = string.Empty;
    public DateTime? TokenExpiration { get; set; }
    public bool IsVerified { get; set; }
    public string? DiditSessionId { get; set; }
    public bool IsActive { get; set; }
    public int FailedAttempts { get; private set; }
    public DateTime? LastLoginAttempt { get; private set; }
    public string? ProfilePic { get; set; }
    public double Points { get; private set; }
    public double PendingBalance { get; set; }
    public string CreditCard { get; private set; } = string.Empty;

    // Métodos de dominio
    public void DeductPoints(double amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        if (Points < amount)
            throw new InvalidOperationException("Insufficient points");
        
        Points -= amount;
    }

    public void AddPoints(double amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        Points += amount;
    }

    public void UpdatePassword(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("Password cannot be empty", nameof(newPassword));
        
        Password = newPassword;
    }

    public void RecordFailedLoginAttempt()
    {
        FailedAttempts++;
        LastLoginAttempt = DateTime.UtcNow;
    }

    public void ResetFailedAttempts()
    {
        FailedAttempts = 0;
    }

    public void UpdateSession()
    {
        LastSession = DateTime.UtcNow;
        TokenExpiration = DateTime.UtcNow.AddDays(15);
    }

    public void UpdateFcm(string fcm)
    {
        if (string.IsNullOrWhiteSpace(fcm))
            throw new ArgumentException("FCM cannot be empty", nameof(fcm));
        
        Fcm = fcm;
    }

    public void UpdateDiditSessionId(string sessionId)
    {
        DiditSessionId = sessionId;
    }
}
