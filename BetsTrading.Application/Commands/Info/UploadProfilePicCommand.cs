using MediatR;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace BetsTrading.Application.Commands.Info;

public class UploadProfilePicCommand : IRequest<UploadProfilePicResult>
{
    // Solo una propiedad para userId que acepta tanto "userId" como "UserId" del JSON
    // Usamos JsonPropertyName para forzar "userId" y PropertyNameCaseInsensitive en Program.cs
    // manejará ambos casos automáticamente
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
    
    public string ProfilePic { get; set; } = string.Empty;
    
    public string GetUserId()
    {
        return UserId ?? string.Empty;
    }
}

public class UploadProfilePicResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
