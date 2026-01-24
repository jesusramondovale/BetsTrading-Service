using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class GoogleLogInCommand : IRequest<GoogleLogInResult>
{
    /// <summary>Google user ID. Binds from "userId" (camelCase) or "UserId" via case-insensitive JSON options.</summary>
    public string? UserId { get; set; }

    public string GetUserId() => UserId ?? string.Empty;
}

public class GoogleLogInResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
