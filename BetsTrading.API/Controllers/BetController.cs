using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BetsTrading.Application.Commands.Bets;
using BetsTrading.Application.Queries.Bets;
using BetsTrading.Application.DTOs;
using BetsTrading.Application.Queries.FinancialAssets;
using BetsTrading.Domain.Exceptions;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BetController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BetController> _logger;

    public BetController(IMediator mediator, ILogger<BetController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("NewBet")]
    public async Task<IActionResult> NewBet([FromBody] CreateBetRequest request)
    {
        try
        {
            _logger.LogDebug("[BetController] :: NewBet :: Request received. UserId: {userId}, BetZoneId: {betZoneId}, Currency: {currency}, BetAmount: {betAmount}", 
                request.UserId, request.BetZoneId, request.Currency ?? "EUR", request.BetAmount);

            var command = new CreateBetCommand
            {
                UserId = request.GetUserId(),
                Fcm = request.GetFcm(),
                Ticker = request.Ticker ?? string.Empty,
                BetAmount = request.GetBetAmount(),
                OriginValue = request.GetOriginValue(),
                BetZoneId = request.GetBetZoneId(),
                Currency = request.Currency ?? "EUR"
            };

            var result = await _mediator.Send(command);
            _logger.LogInformation("[BetController] :: NewBet :: Bet created successfully. BetId: {betId}, RemainingPoints: {points}", 
                result.BetId, result.RemainingPoints);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[BetController] :: NewBet :: InvalidOperationException: {message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (InsufficientPointsException ex)
        {
            _logger.LogWarning("[BetController] :: NewBet :: InsufficientPointsException: {message}", ex.Message);
            return BadRequest(new { Message = "Not enough points" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: NewBet :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("UserBets")]
    public async Task<IActionResult> UserBets([FromBody] GetUserBetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var query = new GetUserBetsQuery
            {
                UserId = finalUserId,
                IncludeArchived = request.IncludeArchived
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(new { bets = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: UserBets :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("HistoricUserBets")]
    public async Task<IActionResult> HistoricUserBets([FromBody] GetUserBetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var query = new GetHistoricUserBetsQuery
            {
                UserId = finalUserId
            };

            var betsResult = await _mediator.Send(query, cancellationToken);
            
            // Also get historic price bets (EUR by default, matching frontend expectation)
            var priceBetsQuery = new GetHistoricPriceBetsQuery
            {
                UserId = finalUserId,
                Currency = "EUR"
            };
            var priceBetsResult = await _mediator.Send(priceBetsQuery, cancellationToken);
            
            if (!betsResult.Any() && !priceBetsResult.Any())
            {
                return NotFound(new { Message = "User has no historic bets!" });
            }
            return Ok(new { Message = "HistoricUserBets SUCCESS", bets = betsResult, priceBets = priceBetsResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: HistoricUserBets :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("GetBetZone")]
    public async Task<IActionResult> GetBetZone([FromBody] GetBetZoneRequest request)
    {
        var query = new GetBetZoneQuery
        {
            BetId = request.GetBetId(),
            Currency = request.Currency ?? "EUR"
        };

        var result = await _mediator.Send(query);
        if (result == null)
        {
            return NotFound(new { Message = "Bet Zone doesn't exist" });
        }
        return Ok(new { bets = new List<BetZoneDto> { result } });
    }

    [HttpPost("DeleteRecentBet")]
    public async Task<IActionResult> DeleteRecentBet([FromBody] DeleteBetRequest request)
    {
        var command = new DeleteRecentBetCommand
        {
            BetId = request.GetBetId()
        };

        var result = await _mediator.Send(command);
        if (!result)
        {
            return NotFound(new { Message = "Bet not found" });
        }
        return Ok(new { });
    }

    [HttpPost("DeleteHistoricBet")]
    public async Task<IActionResult> DeleteHistoricBet([FromBody] GetUserBetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var command = new DeleteHistoricBetsCommand
            {
                UserId = finalUserId
            };

            var deletedCount = await _mediator.Send(command, cancellationToken);
            if (deletedCount == 0)
            {
                return NotFound(new { Message = "No historic bets found for the user" });
            }
            return Ok(new { Message = "All old bets deleted successfully", deletedCount = deletedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: DeleteHistoricBet :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("UserBet")]
    public async Task<IActionResult> UserBet([FromBody] GetUserBetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var query = new GetUserBetQuery
            {
                UserId = finalUserId,
                BetId = request.GetBetId(),
                Currency = request.Currency ?? "EUR"
            };

            var result = await _mediator.Send(query, cancellationToken);
            if (result == null)
            {
                return NotFound(new { Message = "User has no bets!" });
            }
            return Ok(new { Message = "UserBet SUCCESS", bet = new List<BetDto> { result } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: UserBet :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("PriceBets")]
    public async Task<IActionResult> PriceBets([FromBody] GetPriceBetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var query = new GetPriceBetsQuery
            {
                UserId = finalUserId,
                Currency = request.Currency ?? "EUR"
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(new { Message = "UserPriceBets SUCCESS", bets = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: PriceBets :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("HistoricPriceBets")]
    public async Task<IActionResult> HistoricPriceBets([FromBody] GetPriceBetsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate token and get userId from token
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            // Get userId from request
            var requestUserId = request.GetUserId();
            
            // If userId is provided in request, verify it matches the token userId (unless admin)
            if (!string.IsNullOrEmpty(requestUserId) &&
                !string.Equals(requestUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Use token userId if not provided in request
            var finalUserId = !string.IsNullOrEmpty(requestUserId) ? requestUserId : tokenUserId;

            var query = new GetHistoricPriceBetsQuery
            {
                UserId = finalUserId,
                Currency = request.Currency ?? "EUR"
            };

            var result = await _mediator.Send(query, cancellationToken);
            if (!result.Any())
            {
                return NotFound(new { Message = "User has no archived price bets!" });
            }
            return Ok(new { Message = "HistoricPriceBets SUCCESS", bets = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BetController] :: HistoricPriceBets :: Unexpected error: {message}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("NewPriceBet")]
    public async Task<IActionResult> NewPriceBet([FromBody] CreatePriceBetRequest request)
    {
        var command = new CreatePriceBetCommand
        {
            UserId = request.GetUserId(),
            Fcm = request.GetFcm(),
            Ticker = request.Ticker ?? string.Empty,
            PriceBet = request.GetPriceBet(),
            Margin = request.Margin,
            EndDate = request.GetEndDate(),
            Currency = request.Currency ?? "EUR"
        };

        var result = await _mediator.Send(command);
        return Ok(new { });
    }

    [HttpPost("DeleteRecentPriceBet")]
    public async Task<IActionResult> DeleteRecentPriceBet([FromBody] DeletePriceBetRequest request)
    {
        var command = new DeleteRecentPriceBetCommand
        {
            PriceBetId = request.GetPriceBetId(),
            Currency = request.Currency ?? "EUR"
        };

        var result = await _mediator.Send(command);
        if (!result)
        {
            return NotFound(new { Message = "Price bet not found" });
        }
        return Ok(new { });
    }

    [HttpPost("GetBetZones")]
    public async Task<IActionResult> GetBetZones([FromBody] GetBetZonesRequest request, CancellationToken cancellationToken)
    {
        var query = new GetBetZonesQuery
        {
            Ticker = request.Ticker ?? string.Empty,
            Timeframe = request.GetTimeframe(),
            Currency = request.Currency ?? "EUR"
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.Success)
        {
            return NotFound(new { Message = result.Message });
        }

        return Ok(new { bets = result.BetZones });
    }
}

// Request DTOs (temporales - se pueden mover a Application/DTOs)
public class CreateBetRequest
{
    public string? UserId { get; set; }
    public string? Fcm { get; set; }
    public string? Ticker { get; set; }
    public double BetAmount { get; set; }
    public double OriginValue { get; set; }
    public int BetZoneId { get; set; }
    public string? Currency { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
    public string GetFcm() => Fcm ?? string.Empty;
    public double GetBetAmount() => BetAmount;
    public double GetOriginValue() => OriginValue;
    public int GetBetZoneId() => BetZoneId;
}

public class GetUserBetsRequest
{
    public string? UserId { get; set; }
    public bool IncludeArchived { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
}

public class GetBetZoneRequest
{
    public int BetId { get; set; }
    public string? Currency { get; set; }
    public int GetBetId() => BetId;
}

public class DeleteBetRequest
{
    public int BetId { get; set; }
    public int GetBetId() => BetId;
}

public class GetUserBetRequest
{
    public string? UserId { get; set; }
    public int BetId { get; set; }
    public string? Currency { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
    public int GetBetId() => BetId;
}

public class GetPriceBetsRequest
{
    public string? UserId { get; set; }
    public string? Currency { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
}

public class CreatePriceBetRequest
{
    public string? UserId { get; set; }
    public string? Fcm { get; set; }
    public string? Ticker { get; set; }
    public double PriceBet { get; set; }
    public double Margin { get; set; }
    public DateTime EndDate { get; set; }
    public string? Currency { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
    public string GetFcm() => Fcm ?? string.Empty;
    public double GetPriceBet() => PriceBet;
    public DateTime GetEndDate() => EndDate;
}

public class DeletePriceBetRequest
{
    public int PriceBetId { get; set; }
    public string? Currency { get; set; }
    public int GetPriceBetId() => PriceBetId;
}

public class GetBetZonesRequest
{
    public string? Ticker { get; set; }
    public int Timeframe { get; set; }
    public string? Currency { get; set; }
    public int GetTimeframe() => Timeframe;
}
