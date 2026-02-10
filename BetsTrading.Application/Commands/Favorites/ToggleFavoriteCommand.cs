using MediatR;

namespace BetsTrading.Application.Commands.Favorites;

public class ToggleFavoriteCommand : IRequest<ToggleFavoriteResult>
{
    /// <summary>User ID. Binds from "userId" (camelCase) via API options. Controller overwrites with JWT sub.</summary>
    public string? UserId { get; set; }

    public string Ticker { get; set; } = string.Empty;

    public string GetUserId() => UserId ?? string.Empty;
}

public class ToggleFavoriteResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
