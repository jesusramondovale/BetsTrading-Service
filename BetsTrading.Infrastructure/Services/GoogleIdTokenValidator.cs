using BetsTrading.Application.Interfaces;
using Google.Apis.Auth;

namespace BetsTrading.Infrastructure.Services;

public class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly string[] _clientIds;

    public GoogleIdTokenValidator()
    {
        // Varios IDs separados por coma: móvil (serverClientId) + web.
        // Ejemplo: GOOGLE_CLIENT_ID=id-movil.apps...,id-web.apps...
        var raw = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
        _clientIds = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    public async Task<GoogleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || _clientIds.Length == 0)
            return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _clientIds
                });

            return new GoogleIdTokenPayload
            {
                Subject = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                Picture = payload.Picture
            };
        }
        catch
        {
            return null;
        }
    }
}
