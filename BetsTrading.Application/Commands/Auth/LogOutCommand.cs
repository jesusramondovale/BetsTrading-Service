using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class LogOutCommand : IRequest<LogOutResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class LogOutResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
