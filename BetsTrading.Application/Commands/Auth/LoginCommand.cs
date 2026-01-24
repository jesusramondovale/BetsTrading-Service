using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class LoginCommand : IRequest<LoginResult>
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Fcm { get; set; }
    /// <summary>IP del cliente (CF-Connecting-IP / X-Forwarded-For / RemoteIpAddress). Para geo en notificación "otro dispositivo".</summary>
    public string? ClientIp { get; set; }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? UserId { get; set; }
    public string? JwtToken { get; set; }
}
