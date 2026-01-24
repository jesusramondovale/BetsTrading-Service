using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Queries.Info;
using BetsTrading.Application.Queries.Favorites;
using BetsTrading.Application.Commands.Favorites;
using BetsTrading.Application.Commands.WithdrawalMethods;
using BetsTrading.Application.Commands.Raffles;
using BetsTrading.Application.Commands.Info;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using BetsTrading.Application.DTOs;
using System.IO;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InfoController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _env;
    private readonly IApplicationLogger _logger;

    public InfoController(IMediator mediator, IWebHostEnvironment env, IApplicationLogger logger)
    {
        _mediator = mediator;
        _env = env;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("AddAps")]
    public async Task<IActionResult> GetAppAds()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app-ads.txt");

        if (!System.IO.File.Exists(path))
        {
            return NotFound("app-ads.txt not found");
        }

        var content = await System.IO.File.ReadAllTextAsync(path);
        return Content(content, "text/plain");
    }

    [AllowAnonymous]
    [HttpPost("UserInfo")]
    public async Task<IActionResult> UserInfo([FromBody] GetUserInfoQuery? query, CancellationToken cancellationToken)
    {
        try
        {
            // Handle null query (empty body or malformed JSON)
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
                        _logger.Warning("[INFO] :: UserInfo :: Empty request body");
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"id\": \"your-user-id\"}, {\"UserId\": \"your-user-id\"}, or {\"userId\": \"your-user-id\"}" });
                    }
                    
                    // If body exists but model binding failed, it's likely malformed JSON or wrong property name
                    _logger.Warning("[INFO] :: UserInfo :: Invalid JSON format or property name. Body: {body}", rawBody.Substring(0, Math.Min(100, rawBody.Length)));
                    
                    // Try to parse manually to handle "id", "UserId", or "userId"
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(rawBody);
                        string? userId = null;
                        
                        if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                        {
                            userId = idElement.GetString();
                            _logger.Debug("[INFO] :: UserInfo :: Successfully parsed 'id' property from request body");
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("UserId", out var userIdElement))
                        {
                            userId = userIdElement.GetString();
                            _logger.Debug("[INFO] :: UserInfo :: Successfully parsed 'UserId' property from request body");
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("userId", out var userIdLowerElement))
                        {
                            userId = userIdLowerElement.GetString();
                            _logger.Debug("[INFO] :: UserInfo :: Successfully parsed 'userId' property from request body");
                        }
                        
                        if (!string.IsNullOrEmpty(userId))
                        {
                            query = new GetUserInfoQuery { UserId = userId };
                        }
                    }
                    catch
                    {
                        // If manual parsing fails, return error
                        return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"id\": \"your-user-id\"} or {\"UserId\": \"your-user-id\"}" });
                    }
                    
                    if (query == null)
                    {
                        return BadRequest(new { Message = "Request body must contain 'id', 'UserId', or 'userId' property" });
                    }
                }
                catch
                {
                    _logger.Warning("[INFO] :: UserInfo :: Request body is required");
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"id\": \"your-user-id\"}, {\"UserId\": \"your-user-id\"}, or {\"userId\": \"your-user-id\"}" });
                }
            }

            // Try to get user ID from token (if authenticated)
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            
            // Log todas las claims para debugging
            var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
            _logger.Debug("[INFO] :: UserInfo :: IsAuthenticated: {0}, TokenUserId: {1}, ClaimsCount: {2}, AllClaims: {3}", 
                isAuthenticated, tokenUserId ?? "null", User.Claims.Count(), string.Join(", ", allClaims));

            // Use token user ID if not provided in query
            if (string.IsNullOrEmpty(query.UserId))
            {
                if (!string.IsNullOrEmpty(tokenUserId))
                {
                    query.UserId = tokenUserId;
                    _logger.Debug("[INFO] :: UserInfo :: Using user ID from token: {tokenUserId}", tokenUserId);
                }
                else
                {
                    // If no token and no user ID in query, return error
                    _logger.Warning("[INFO] :: UserInfo :: User ID is required (not in request body and no token)");
                    return BadRequest(new { Message = "User ID is required" });
                }
            }
            // If token is present and valid, verify it matches the requested user ID
            else if (!string.IsNullOrEmpty(tokenUserId))
            {
                if (!string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                    !User.IsInRole("admin"))
                {
                    // 403 FORBIDDEN : User ID doesn't match JWT
                    _logger.Warning("[INFO] :: UserInfo :: User ID mismatch. Token: {tokenId}, Request: {requestId}", tokenUserId, query.UserId);
                    return Forbid();
                }
            }
            // If no token, we'll still allow the request (like IsLoggedIn does)
            // This allows the endpoint to work even if token validation fails

            var result = await _mediator.Send(query, cancellationToken);

            if (result == null)
            {
                return NotFound(new { Message = "User or email not found" });
            }

            return Ok(new
            {
                Message = "UserInfo SUCCESS",
                username = result.Username,
                isverified = result.IsVerified,
                email = result.Email,
                birthday = result.Birthday,
                fullname = result.Fullname,
                country = result.Country,
                lastsession = result.LastSession,
                profilepic = result.ProfilePic,
                points = result.Points
            });
        }
        catch (System.Text.Json.JsonException)
        {
            // Handle JSON parsing errors specifically
            return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("Favorites")]
    public async Task<IActionResult> Favorites([FromBody] GetFavoritesQuery? query, CancellationToken cancellationToken)
    {
        try
        {
            // Handle null query (empty body or malformed JSON)
            if (query == null)
            {
                query = new GetFavoritesQuery();
            }

            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(query.UserId) &&
                !string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Always use user ID from token (security)
            query.UserId = tokenUserId;

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(new
            {
                Message = "Favorites SUCCESS",
                favorites = result.Favorites
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[INFO] :: Favorites :: Internal server error: {0}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("NewFavorite")]
    public async Task<IActionResult> NewFavorite([FromBody] ToggleFavoriteCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = tokenUserId;

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new { Message = result.Message });
            }

            return Ok(new { });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("Trends")]
    public async Task<IActionResult> Trends([FromBody] GetTrendsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var queryUserId = query.GetUserId();
            if (!string.IsNullOrEmpty(queryUserId) &&
                !string.Equals(queryUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(queryUserId))
            {
                query.UserId = tokenUserId;
            }
            else
            {
                query.UserId = queryUserId;
            }

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(new
            {
                Message = "Trends SUCCESS",
                trends = result.Trends
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("TopUsers")]
    public async Task<IActionResult> TopUsers([FromBody] GetTopUsersQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(query.UserId) &&
                !string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(new
            {
                Message = "TopUsers SUCCESS",
                users = result.Users
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("TopUsersByCountry")]
    public async Task<IActionResult> TopUsersByCountry([FromBody] GetTopUsersByCountryQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var countryCode = query.GetCountryCode();
            if (string.IsNullOrEmpty(countryCode))
            {
                return BadRequest(new { Message = "Country code is required" });
            }
            
            query.CountryCode = countryCode;
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(new
            {
                Message = "TopUsersByCountry SUCCESS",
                users = result.Users
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("PendingBalance")]
    public async Task<IActionResult> PendingBalance([FromBody] GetPendingBalanceQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(query.UserId) &&
                !string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            query.UserId = tokenUserId;

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            if (result.PasswordNotSet)
            {
                return StatusCode(StatusCodes.Status201Created, new { Message = result.Message });
            }

            return Ok(new { balance = result.Balance });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("PaymentHistory")]
    public async Task<IActionResult> PaymentHistory([FromBody] GetPaymentHistoryQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(query.UserId) &&
                !string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            query.UserId = tokenUserId;

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(result.Payments);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("WithdrawalHistory")]
    public async Task<IActionResult> WithdrawalHistory([FromBody] GetWithdrawalHistoryQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.IsNullOrEmpty(query.UserId) &&
                !string.Equals(query.UserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            query.UserId = tokenUserId;

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(result.Withdrawals);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("StoreOptions")]
    public IActionResult StoreOptions([FromBody] GetStoreOptionsQuery? query)
    {
        try
        {
            var currency = (query?.Currency ?? "eur").ToLowerInvariant();
            var type = query?.Type ?? "buy";
            var fileName = $"exchange_options_{currency}.json";

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!System.IO.File.Exists(path))
            {
                path = Path.Combine(_env.ContentRootPath, fileName);
            }
            if (!System.IO.File.Exists(path))
            {
                _logger.Debug("StoreOptions: exchange options file not found. Tried BaseDirectory and ContentRootPath for {0}", fileName);
                return NotFound(new { Message = "Exchange options file not found", File = fileName });
            }

            var json = System.IO.File.ReadAllText(path);
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<StoreOptionDto>>(json, options);
            if (parsed == null)
            {
                return Ok(new List<StoreOptionDto>());
            }

            var filtered = parsed
                .Where(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "StoreOptions: error reading or filtering exchange options");
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("RetireOptions")]
    public async Task<IActionResult> RetireOptions([FromBody] GetRetireOptionsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            query.UserId = tokenUserId;

            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }

            return Ok(result.Options);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("DeleteRetireOption")]
    public async Task<IActionResult> DeleteRetireOption([FromBody] DeleteWithdrawalMethodCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
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

            return Ok(new { });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("AddBankRetireMethod")]
    public async Task<IActionResult> AddBankRetireMethod([FromBody] AddBankWithdrawalMethodCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = tokenUserId;

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new { Message = result.Message });
            }

            return Ok(new { id = result.Id, created = result.Created, verified = result.Verified });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("AddPaypalRetireMethod")]
    public async Task<IActionResult> AddPaypalRetireMethod([FromBody] AddPaypalWithdrawalMethodCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = tokenUserId;

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new { Message = result.Message });
            }

            return Ok(new { id = result.Id, created = result.Created, verified = result.Verified });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("AddCryptoRetireMethod")]
    public async Task<IActionResult> AddCryptoRetireMethod([FromBody] AddCryptoWithdrawalMethodCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = tokenUserId;

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new { Message = result.Message });
            }

            return Ok(new { id = result.Id, created = result.Created, verified = result.Verified });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("RaffleItems")]
    public async Task<IActionResult> RaffleItems([FromBody] GetRaffleItemsQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var userId = query.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { Error = "Missing or invalid user id" });
            }

            query.UserId = userId;
            var result = await _mediator.Send(query, cancellationToken);

            if (!result.Success)
            {
                if (result.Message == "No raffle items found")
                {
                    return NoContent();
                }
                return NotFound(new { Error = result.Message });
            }

            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("NewRaffle")]
    public async Task<IActionResult> NewRaffle([FromBody] CreateRaffleCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var userId = command.GetUserId();
            var itemToken = command.GetItemToken();
            
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(itemToken))
            {
                return BadRequest(new { Error = "Missing or invalid user id or item token" });
            }

            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            if (!string.Equals(userId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = userId;
            command.ItemToken = itemToken;
            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                if (result.Message == "Not enough points")
                {
                    return BadRequest(new { Error = result.Message });
                }
                return NotFound(new { Error = result.Message });
            }

            // Get updated user points
            var user = await _mediator.Send(new GetUserInfoQuery { UserId = userId }, cancellationToken);

            return Ok(new
            {
                raffleId = int.Parse(itemToken),
                userPoints = user?.Points ?? 0
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("UploadPic")]
    public async Task<IActionResult> UploadPic([FromBody] UploadProfilePicCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue("app_sub") 
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(tokenUserId))
            {
                return Unauthorized(new { Message = "Invalid token" });
            }

            var commandUserId = command.GetUserId();
            if (!string.IsNullOrEmpty(commandUserId) &&
                !string.Equals(commandUserId, tokenUserId, StringComparison.Ordinal) &&
                !User.IsInRole("admin"))
            {
                return Forbid();
            }

            command.UserId = tokenUserId;

            var result = await _mediator.Send(command, cancellationToken);

            if (!result.Success)
            {
                if (result.Message.Contains("No active session"))
                {
                    return BadRequest(new { Message = result.Message });
                }
                return NotFound(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, userId = result.UserId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }
}
