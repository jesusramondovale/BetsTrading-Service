using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class RegisterCommand : IRequest<RegisterResult>
{
    public string? Token { get; set; } // Google ID
    public string Fcm { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? Country { get; set; }
    public string? Gender { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? EmailCode { get; set; }
    public DateTime? Birthday { get; set; }
    public string? CreditCard { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ProfilePic { get; set; }
    public bool GoogleQuickMode { get; set; } = false;
}

public class RegisterResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? JwtToken { get; set; }
}
