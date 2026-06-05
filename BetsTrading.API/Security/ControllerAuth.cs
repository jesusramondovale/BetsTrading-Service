using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace BetsTrading.API.Security;

public static class ControllerAuth
{
    public static string? GetTokenUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("app_sub")
        ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("admin");

    public static IActionResult? RequireAuthenticatedUser(ClaimsPrincipal user)
    {
        if (string.IsNullOrEmpty(GetTokenUserId(user)))
            return new UnauthorizedObjectResult(new { Message = "Invalid token" });
        return null;
    }

    public static IActionResult? ForbidIfUserMismatch(ClaimsPrincipal user, string? requestUserId)
    {
        var tokenUserId = GetTokenUserId(user);
        if (!string.IsNullOrEmpty(requestUserId) &&
            !string.IsNullOrEmpty(tokenUserId) &&
            !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
            !IsAdmin(user))
            return new ForbidResult();
        return null;
    }

    public static string RequireAndResolveUserId(ClaimsPrincipal user, string? requestUserId)
    {
        var tokenUserId = GetTokenUserId(user)!;
        return !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;
    }
}
