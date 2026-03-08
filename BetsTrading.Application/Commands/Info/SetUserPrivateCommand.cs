using MediatR;
using System.Text.Json.Serialization;

namespace BetsTrading.Application.Commands.Info;

public class SetUserPrivateCommand : IRequest<SetUserPrivateResult>
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("isPrivate")]
    public bool IsPrivate { get; set; }

    public string GetUserId() => UserId ?? string.Empty;
}

public class SetUserPrivateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
