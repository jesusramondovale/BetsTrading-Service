using MediatR;
using System.Text.Json.Serialization;

namespace BetsTrading.Application.Commands.Auth;

public class RefreshFcmCommand : IRequest<RefreshFcmResult>
{
    // Support both "UserId" (PascalCase) and "user_id" (snake_case from Flutter client)
    [JsonPropertyName("UserId")]
    public string? UserId { get; set; }
    
    [JsonPropertyName("user_id")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? UserIdSnake { get; set; }
    
    // Support both "Fcm" (PascalCase), "fcm" (camelCase), and "token" (legacy from Flutter client)
    // Use "token" as primary JSON property (client format) and map to Fcm internally
    [JsonPropertyName("token")]
    public string? FcmFromToken { get; set; }
    
    [JsonPropertyName("fcm")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? FcmLower { get; set; }
    
    // Internal property for PascalCase (will be set manually if needed)
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Fcm { get; set; }

    /// <summary>IP del cliente (CF-Connecting-IP / X-Forwarded-For / RemoteIpAddress). Para geo en notificación "otro dispositivo".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ClientIp { get; set; }
    
    // Computed property that returns the actual user ID from either property
    public string GetUserId()
    {
        if (!string.IsNullOrEmpty(UserId))
            return UserId;
        if (!string.IsNullOrEmpty(UserIdSnake))
            return UserIdSnake;
        return string.Empty;
    }
    
    // Computed property that returns the actual FCM token from any property
    public string GetFcm()
    {
        // First check the PascalCase property (set manually)
        if (!string.IsNullOrEmpty(Fcm))
            return Fcm;
        // Then check "token" (client format)
        if (!string.IsNullOrEmpty(FcmFromToken))
            return FcmFromToken;
        // Then check "fcm" (camelCase)
        if (!string.IsNullOrEmpty(FcmLower))
            return FcmLower;
        return string.Empty;
    }
}

public class RefreshFcmResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
