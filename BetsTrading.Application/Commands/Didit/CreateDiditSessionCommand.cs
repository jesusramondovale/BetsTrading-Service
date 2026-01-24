using MediatR;

namespace BetsTrading.Application.Commands.Didit;

public class CreateDiditSessionCommand : IRequest<CreateDiditSessionResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class CreateDiditSessionResult
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public System.Text.Json.JsonElement? Response { get; set; }
    public string? Message { get; set; }
}
