using System.Linq;
using System.Net;
using System.Text.Json;
using BetsTrading.Domain.Exceptions;

namespace BetsTrading.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError;
        var result = string.Empty;

        switch (exception)
        {
            case FluentValidation.ValidationException validationEx:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage)),
                    errors = validationEx.Errors.Select(e => e.ErrorMessage).ToArray()
                });
                break;
            case InvalidOperationException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new { success = false, message = exception.Message });
                break;
            case InsufficientPointsException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new { success = false, message = exception.Message });
                break;
            case BetException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new { success = false, message = exception.Message });
                break;
            case FormatException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new { success = false, message = "Invalid request format. Please check your request body and Content-Type header." });
                break;
            case System.Text.Json.JsonException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new { success = false, message = "Invalid JSON format in request body." });
                break;
            default:
                result = JsonSerializer.Serialize(new { success = false, message = "An error occurred while processing your request.", error = exception?.Message ?? "" });
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;

        return context.Response.WriteAsync(result);
    }
}
