using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class NewPasswordCommand : IRequest<NewPasswordResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class NewPasswordResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
