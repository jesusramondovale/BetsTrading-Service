using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Commands.Didit;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiditController : ControllerBase
{
    private readonly IMediator _mediator;

    public DiditController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("CreateSession")]
    public async Task<IActionResult> CreateSession([FromBody] CreateDiditSessionCommand command, CancellationToken cancellationToken)
    {
        // Extract user ID from token or body
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        // Use token user ID or allow admin override
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
            if (result.Message == "User not found")
            {
                return NotFound(new { Message = result.Message });
            }
            return StatusCode(500, new { Message = result.Message ?? "Failed to create session" });
        }

        // Return the raw response from Didit
        if (result.Response.HasValue)
        {
            return Ok(result.Response.Value);
        }

        return Ok(new { sessionId = result.SessionId });
    }

    [AllowAnonymous]
    [HttpPost("Webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        JsonElement payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return BadRequest(new { Message = "Invalid JSON" });
        }

        var command = new ProcessDiditWebhookCommand
        {
            Payload = payload
        };

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "User not found")
            {
                return NotFound(new { Message = result.Message });
            }
            if (result.Message == "User under legal age")
            {
                return StatusCode(500, new { Message = result.Message });
            }
            return BadRequest(new { Message = result.Message ?? "Failed to process webhook" });
        }

        return Ok(new { Message = result.Message });
    }
}
