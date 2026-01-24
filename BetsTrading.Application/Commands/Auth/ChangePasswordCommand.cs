using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class ChangePasswordCommand : IRequest<ChangePasswordResult>
{
    public string UserId { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
