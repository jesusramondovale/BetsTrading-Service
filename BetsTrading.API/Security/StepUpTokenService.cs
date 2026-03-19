using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace BetsTrading.API.Security;

public interface IStepUpTokenService
{
    string IssueToken(string userId, string purpose, double? maxAmountCoins = null);
    bool ValidateAndConsume(string token, string expectedUserId, string expectedPurpose, double? requiredAmountCoins = null);
}

public class StepUpTokenService : IStepUpTokenService
{
    private readonly IMemoryCache _cache;
    private readonly string _issuer;
    private readonly SymmetricSecurityKey _signingKey;
    private const string Audience = "bets-trading-stepup";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);

    public StepUpTokenService(IMemoryCache cache, string issuer, string signingKey)
    {
        _cache = cache;
        _issuer = issuer;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public string IssueToken(string userId, string purpose, double? maxAmountCoins = null)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        var jti = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("purpose", normalizedPurpose),
            new(JwtRegisteredClaimNames.Jti, jti),
        };

        if (maxAmountCoins.HasValue)
        {
            claims.Add(new Claim("maxCoins", maxAmountCoins.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(DefaultTtl),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        );

        var tokenText = new JwtSecurityTokenHandler().WriteToken(token);
        _cache.Set(BuildCacheKey(jti), true, DefaultTtl);
        return tokenText;
    }

    public bool ValidateAndConsume(string token, string expectedUserId, string expectedPurpose, double? requiredAmountCoins = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        ClaimsPrincipal principal;
        try
        {
            principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(10)
            }, out _);
        }
        catch
        {
            return false;
        }

        var tokenUserId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenPurpose = NormalizePurpose(principal.FindFirstValue("purpose") ?? "");
        var expectedPurposeNormalized = NormalizePurpose(expectedPurpose);
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

        if (string.IsNullOrWhiteSpace(tokenUserId) || string.IsNullOrWhiteSpace(jti))
            return false;
        if (!string.Equals(tokenUserId, expectedUserId, StringComparison.Ordinal))
            return false;
        if (!string.Equals(tokenPurpose, expectedPurposeNormalized, StringComparison.Ordinal))
            return false;

        if (requiredAmountCoins.HasValue)
        {
            var maxCoinsStr = principal.FindFirstValue("maxCoins");
            if (!double.TryParse(maxCoinsStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maxCoins))
                return false;
            if (requiredAmountCoins.Value > maxCoins)
                return false;
        }

        var cacheKey = BuildCacheKey(jti);
        if (!_cache.TryGetValue(cacheKey, out _))
            return false;

        _cache.Remove(cacheKey); // single-use token
        return true;
    }

    private static string BuildCacheKey(string jti) => $"stepup:{jti}";

    private static string NormalizePurpose(string purpose) => purpose.Trim().ToLowerInvariant();
}
