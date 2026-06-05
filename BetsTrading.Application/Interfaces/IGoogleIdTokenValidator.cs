namespace BetsTrading.Application.Interfaces;

public sealed class GoogleIdTokenPayload
{
    public string Subject { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Name { get; init; }
    public string? Picture { get; init; }
}

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
