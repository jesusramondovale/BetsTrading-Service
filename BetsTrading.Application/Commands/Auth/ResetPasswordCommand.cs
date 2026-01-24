using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class ResetPasswordCommand : IRequest<ResetPasswordResult>
{
    public string EmailOrId { get; set; } = string.Empty;
}

public class ResetPasswordResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
