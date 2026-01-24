using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Commands.Auth;
using BetsTrading.Application.Queries.Auth;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.IO;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [AllowAnonymous]
    [HttpPost("LogIn")]
    public async Task<IActionResult> LogIn([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        command.ClientIp = HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new 
        { 
            success = true, 
            message = result.Message, 
            userId = result.UserId,
            jwtToken = result.JwtToken 
        });
    }

    [AllowAnonymous]
    [HttpPost("SendCode")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return Conflict(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [AllowAnonymous]
    [HttpPost("SignIn")]
    public async Task<IActionResult> SignIn([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        command.GoogleQuickMode = false;
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            if (result.Message.Contains("already exists"))
                return Conflict(new { success = false, message = result.Message });
            
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new 
        { 
            success = true, 
            message = result.Message, 
            userId = result.UserId,
            jwtToken = result.JwtToken 
        });
    }

    [AllowAnonymous]
    [HttpPost("GoogleQuickRegister")]
    public async Task<IActionResult> GoogleQuickRegister([FromBody] GoogleSignInCommand command, CancellationToken cancellationToken)
    {
        var registerCommand = new RegisterCommand
        {
            Token = command.Id,
            Fcm = command.Fcm ?? "-",
            FullName = command.DisplayName ?? "-",
            Email = command.Email ?? "-",
            Username = !string.IsNullOrEmpty(command.Email) 
                ? command.Email.Split('@')[0] 
                : command.DisplayName ?? "user",
            ProfilePic = command.PhotoUrl,
            Birthday = command.Birthday,
            Country = command.Country,
            GoogleQuickMode = true
        };

        var result = await _mediator.Send(registerCommand, cancellationToken);
        
        if (!result.Success)
        {
            return StatusCode(500, new { Message = "Internal server error", Error = result.Message });
        }

        return Ok(new { message = "User quick-registered", userId = result.UserId });
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        // Get user ID from JWT token
        var tokenUserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        command.UserId = tokenUserId;
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [AllowAnonymous]
    [HttpPost("ResetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return NotFound(new { Message = result.Message });
        }

        return Ok(new { Message = result.Message });
    }

    [HttpPost("NewPassword")]
    public async Task<IActionResult> NewPassword([FromBody] NewPasswordCommand command, CancellationToken cancellationToken)
    {
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        if (!string.IsNullOrEmpty(command.UserId) &&
            !string.Equals(command.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
            return Forbid();
        }

        command.UserId = tokenUserId;

        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { Message = result.Message });
        }

        return Ok(new { Message = result.Message });
    }

    [HttpPost("GoogleLogIn")]
    public async Task<IActionResult> GoogleLogIn([FromBody] GoogleLogInCommand? command, CancellationToken cancellationToken)
    {
        try
        {
            // Handle model binding errors (e.g., malformed JSON, empty body)
            if (command == null)
            {
                // Try to read raw body to provide better error message
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var rawBody = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    
                    if (string.IsNullOrWhiteSpace(rawBody))
                    {
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"UserId\": \"your-google-user-id\"} or {\"userId\": \"your-google-user-id\"}" });
                    }
                    
                    // Try to parse manually to handle "userId" vs "UserId"
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(rawBody);
                        string? parsedUserId = null;
                        
                        if (jsonDoc.RootElement.TryGetProperty("UserId", out var userIdElement))
                        {
                            parsedUserId = userIdElement.GetString();
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("userId", out var userIdLowerElement))
                        {
                            parsedUserId = userIdLowerElement.GetString();
                        }
                        
                        if (!string.IsNullOrEmpty(parsedUserId))
                        {
                            command = new GoogleLogInCommand { UserId = parsedUserId };
                        }
                        else
                        {
                            return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"UserId\": \"your-google-user-id\"} or {\"userId\": \"your-google-user-id\"}", Body = rawBody.Substring(0, Math.Min(100, rawBody.Length)) });
                        }
                    }
                    catch
                    {
                        return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"UserId\": \"your-google-user-id\"} or {\"userId\": \"your-google-user-id\"}", Body = rawBody.Substring(0, Math.Min(100, rawBody.Length)) });
                    }
                }
                catch
                {
                    return BadRequest(new { Message = "Request body is required. Expected JSON: {\"UserId\": \"your-google-user-id\"} or {\"userId\": \"your-google-user-id\"}" });
                }
            }

            // Validate required fields using GetUserId()
            var userId = command.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { Message = "UserId is required" });
            }

            // Update command with resolved userId
            command.UserId = userId;
            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, UserId = result.UserId });
        }
        catch (System.Text.Json.JsonException)
        {
            // Handle JSON parsing errors specifically
            return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
        }
        catch (Exception)
        {
            // Log the exception but don't expose internal details
            return StatusCode(500, new { Message = "Server error", Error = "An error occurred while processing your request." });
        }
    }

    [HttpPost("LogOut")]
    public async Task<IActionResult> LogOut([FromBody] LogOutCommand command, CancellationToken cancellationToken)
    {
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        if (!string.IsNullOrEmpty(command.UserId) &&
            !string.Equals(command.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
            return Forbid();
        }

        command.UserId = tokenUserId;

        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return NotFound(new { Message = result.Message });
        }

        return Ok(new { Message = result.Message, UserId = result.UserId });
    }

    [AllowAnonymous]
    [HttpPost("IsLoggedIn")]
    public async Task<IActionResult> IsLoggedIn([FromBody] IsLoggedInQuery? query, CancellationToken cancellationToken)
    {
        try
        {
            // Handle model binding errors (e.g., malformed JSON, empty body)
            if (query == null)
            {
                // Try to read raw body to provide better error message
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var rawBody = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    
                    if (string.IsNullOrWhiteSpace(rawBody))
                    {
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"UserId\": \"your-user-id\"}" });
                    }
                    
                    // If body exists but model binding failed, it's likely malformed JSON
                    return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"UserId\": \"your-user-id\"}", Body = rawBody.Substring(0, Math.Min(100, rawBody.Length)) });
                }
                catch
                {
                    return BadRequest(new { Message = "Request body is required. Expected JSON: {\"UserId\": \"your-user-id\"}" });
                }
            }

            // Handle case where both UserId and Id properties might be missing (model binding succeeded but properties are null/empty)
            // Note: The IsLoggedInQuery now supports both "UserId" and "id" properties for backward compatibility
            var userId = query.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "UserId or id is required. Expected JSON: {\"UserId\": \"your-user-id\"} or {\"id\": \"your-user-id\"}" });
            }

            var result = await _mediator.Send(query, cancellationToken);
            
            if (!result.Success)
            {
                if (result.PasswordNotSet)
                {
                    return StatusCode(StatusCodes.Status201Created, new { Message = result.Message });
                }
                if (result.Message == "Token not found")
                {
                    return NotFound(new { Message = result.Message });
                }
                return BadRequest(new { Message = result.Message });
            }

            if (result.PasswordNotSet)
            {
                return StatusCode(StatusCodes.Status201Created, new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, UserId = result.UserId });
        }
        catch (System.Text.Json.JsonException)
        {
            // Handle JSON parsing errors specifically
            return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
        }
        catch (Exception)
        {
            // Log the exception but don't expose internal details
            return StatusCode(500, new { Message = "Server error", Error = "An error occurred while processing your request." });
        }
    }

    [Authorize]
    [HttpPost("RefreshFCM")]
    public async Task<IActionResult> RefreshFCM([FromBody] RefreshFcmCommand? command, CancellationToken cancellationToken)
    {
        try
        {
            // Handle null command
            if (command == null)
            {
                command = new RefreshFcmCommand();
            }
            
            // Use computed properties to get the actual values
            var userId = command.GetUserId();
            var fcm = command.GetFcm();
            
            // Set the resolved values in the command for the handler (using Fcm property, not FcmFromToken)
            command.UserId = userId;
            command.Fcm = fcm; // This sets the internal Fcm property
            
            // Handle empty Fcm (empty body, malformed JSON, or model binding failed)
            if (string.IsNullOrEmpty(command.Fcm))
            {
                // Try to read raw body to provide better error message
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Position = 0;
                    using var reader = new StreamReader(Request.Body, leaveOpen: true);
                    var rawBody = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    
                    if (string.IsNullOrWhiteSpace(rawBody))
                    {
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"token\": \"fcm-token\"} or {\"fcm\": \"fcm-token\"}" });
                    }
                    
                    // Try to parse manually to handle both formats
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(rawBody);
                        command ??= new RefreshFcmCommand();
                        
                        // Try "token" first (client format: {'user_id': userId, 'token': token})
                        if (jsonDoc.RootElement.TryGetProperty("token", out var tokenElement))
                        {
                            command.Fcm = tokenElement.GetString() ?? string.Empty;
                        }
                        // Try PascalCase (Fcm)
                        else if (jsonDoc.RootElement.TryGetProperty("Fcm", out var fcmElement))
                        {
                            command.Fcm = fcmElement.GetString() ?? string.Empty;
                        }
                        // Try camelCase/snake_case (fcm)
                        else if (jsonDoc.RootElement.TryGetProperty("fcm", out var fcmElement2))
                        {
                            command.Fcm = fcmElement2.GetString() ?? string.Empty;
                        }
                        
                        // UserId is optional, will be set from token
                        if (jsonDoc.RootElement.TryGetProperty("UserId", out var userIdElement))
                        {
                            command.UserId = userIdElement.GetString() ?? string.Empty;
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("user_id", out var userIdElement2))
                        {
                            command.UserId = userIdElement2.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"token\": \"fcm-token\"} or {\"fcm\": \"fcm-token\"}" });
                    }
                    
                    if (string.IsNullOrEmpty(command.Fcm))
                    {
                        return BadRequest(new { Message = "Request body must contain 'token' or 'fcm' property" });
                    }
                }
                catch
                {
                    return BadRequest(new { Message = "Request body is required. Expected JSON: {\"token\": \"fcm-token\"} or {\"fcm\": \"fcm-token\"}" });
                }
            }

            // Get user ID from token - with [Authorize], authentication should be complete
            // But OnTokenValidated is async, so we may need to wait a bit for claims to be added
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            // If claims are not available yet (OnTokenValidated is still running), wait a bit
            if (string.IsNullOrEmpty(tokenUserId) && User.Identity?.IsAuthenticated == true)
            {
                // Wait for OnTokenValidated to complete (max 500ms)
                for (int i = 0; i < 5 && string.IsNullOrEmpty(tokenUserId); i++)
                {
                    await Task.Delay(100, cancellationToken);
                    tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                        ?? User.FindFirstValue("app_sub") 
                        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                }
            }

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(command.UserId) &&
                !string.Equals(command.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Always use user ID from token (security)
            command.UserId = tokenUserId;

            command.ClientIp = HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _mediator.Send(command, cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, UserId = tokenUserId });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }
}
