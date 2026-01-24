using MediatR;
using System.Text.Json;

namespace BetsTrading.Application.Commands.Didit;

public class ProcessDiditWebhookCommand : IRequest<ProcessDiditWebhookResult>
{
    public JsonElement Payload { get; set; }
}

public class ProcessDiditWebhookResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
