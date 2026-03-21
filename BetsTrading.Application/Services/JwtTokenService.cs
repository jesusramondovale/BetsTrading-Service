using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Services;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, string? name);
}

public class JwtTokenService : IJwtTokenService
{
    private const int DefaultExpirationHours = 96;

    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    private readonly IAdminRuntimeConfig _adminRuntimeConfig;

    public JwtTokenService(
        string issuer,
        string audience,
        string key,
        IAdminRuntimeConfig adminRuntimeConfig)
    {
        _issuer = issuer;
        _audience = audience;
        _key = key;
        _adminRuntimeConfig = adminRuntimeConfig;
    }

    public string GenerateToken(string userId, string email, string? name)
    {
        var expirationHours = _adminRuntimeConfig.JwtTokenExpirationHours ?? DefaultExpirationHours;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Email, email ?? ""),
            new Claim("name", name ?? ""),
            new Claim("auth_provider", "local"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(expirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
