using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace BetsTrading.API.Health;

internal static class AdminAuthHelper
{
    public static bool TryValidateAdminToken(HttpContext ctx, string jwtKey, string issuer, out ClaimsPrincipal? principal)
    {
        principal = null;
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;
        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidIssuer = issuer,
                ValidAudience = "bets-trading-admin",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            principal = handler.ValidateToken(token, validation, out _);
            return principal?.FindFirst("is_admin")?.Value == "true";
        }
        catch
        {
            return false;
        }
    }
}
