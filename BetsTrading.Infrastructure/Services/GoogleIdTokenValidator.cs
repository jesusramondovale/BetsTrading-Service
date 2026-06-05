using BetsTrading.Application.Interfaces;
using Google.Apis.Auth;

namespace BetsTrading.Infrastructure.Services;

public class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly string _clientId;

    public GoogleIdTokenValidator()
    {
        _clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
    }

    public async Task<GoogleIdTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(_clientId))
            return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _clientId }
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
