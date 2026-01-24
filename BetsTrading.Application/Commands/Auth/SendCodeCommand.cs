using MediatR;

namespace BetsTrading.Application.Commands.Auth;

public class SendCodeCommand : IRequest<SendCodeResult>
{
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = "UK";
}

public class SendCodeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
