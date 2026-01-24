using MediatR;
using System.Text.Json.Serialization;

namespace BetsTrading.Application.Queries.Auth;

public class IsLoggedInQuery : IRequest<IsLoggedInResult>
{
    // Support both "UserId" (new format) and "id" (legacy format from Flutter client)
    [JsonPropertyName("UserId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserId { get; set; }
    
    [JsonPropertyName("id")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    
    // Computed property that returns the actual user ID from either property
    public string GetUserId()
    {
        if (!string.IsNullOrEmpty(UserId))
            return UserId;
        if (!string.IsNullOrEmpty(Id))
            return Id;
        return string.Empty;
    }
}

public class IsLoggedInResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; }
    public bool PasswordNotSet { get; set; }
    public string? UserId { get; set; }
}
