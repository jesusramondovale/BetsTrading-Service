using BetsTrading.Application.Interfaces;

namespace BetsTrading.API.Middleware;

/// <summary>
/// Bloquea el acceso a la API de la app cuando el administrador activa el cierre global en /status.
/// </summary>
public sealed class AccessLockMiddleware
{
    private static readonly PathString ApiPrefix = new("/api");

    private readonly RequestDelegate _next;

    public AccessLockMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAdminRuntimeConfig adminConfig)
    {
        if (adminConfig.AccessLockEnabled && IsBlockedApiRequest(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                accessLocked = true,
                message = "Service temporarily unavailable. Please try again later.",
            });
            return;
        }

        await _next(context);
    }

    private static bool IsBlockedApiRequest(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var path = request.Path.Value ?? string.Empty;

        // Webhooks y callbacks externos siguen operativos durante el bloqueo.
        if (path.Contains("/Webhook", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
