using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Commands.Rewards;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RewardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RewardsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("RequestAdNonce")]
    [Consumes("application/json")]
    public async Task<IActionResult> RequestAdNonce([FromBody] RequestAdNonceCommand? command, CancellationToken cancellationToken)
    {
        if (command == null)
        {
            return BadRequest(new { Message = "Request body required. Expected JSON: { \"adUnitId\": \"...\", \"purpose\": \"...\", \"coins\": 20 }" });
        }

        // Extract user ID from header or token
        var userId = Request.Headers["X-UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Try to get from JWT token
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { Message = "missing user id" });
        }

        if (string.IsNullOrWhiteSpace(command.AdUnitId))
        {
            return BadRequest(new { Message = "ad_unit_id required" });
        }

        command.UserId = userId;
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "too_many_pending_nonces")
            {
                return StatusCode(429, new { Message = result.Message });
            }
            return BadRequest(new { Message = result.Message ?? "Failed to create nonce" });
        }

        return Ok(new
        {
            nonce = result.Nonce,
            expiresAt = result.ExpiresAt
        });
    }
}
