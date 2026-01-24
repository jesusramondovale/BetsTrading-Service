using MediatR;
using System.Text.Json.Serialization;

namespace BetsTrading.Application.Commands.Favorites;

public class ToggleFavoriteCommand : IRequest<ToggleFavoriteResult>
{
    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }
    
    [JsonPropertyName("userId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserIdLower { get; set; }
    
    public string Ticker { get; set; } = string.Empty;
    
    public string GetUserId()
    {
        if (!string.IsNullOrEmpty(UserId))
            return UserId;
        if (!string.IsNullOrEmpty(UserIdLower))
            return UserIdLower;
        return string.Empty;
    }
}

public class ToggleFavoriteResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
