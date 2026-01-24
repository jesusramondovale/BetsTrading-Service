using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleSignInCommand : IRequest<GoogleSignInResult>
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Fcm { get; set; }
    public DateTime? Birthday { get; set; }
    public string? Country { get; set; }
}

public class GoogleSignInResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
