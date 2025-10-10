﻿using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Stripe.Forwarding;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class InfoController : ControllerBase
  {
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    private readonly IWebHostEnvironment _env;

    public InfoController(IWebHostEnvironment env, AppDbContext dbContext, ICustomLogger customLogger)
    {
      _env = env;
      _dbContext = dbContext;
      _logger = customLogger;

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

    [HttpPost("UserInfo")]
    public IActionResult UserInfo([FromBody] idRequest userInfoRequest)
    {
      try
      {
        var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("app_sub") ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(userInfoRequest.id) &&
            !string.Equals(userInfoRequest.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }


        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == userInfoRequest.id);

        if (user != null) // User exists
        {
          
          _logger.Log.Debug("[INFO] :: UserInfo :: Success on ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "UserInfo SUCCESS",
            Username = user.username,
            Isverified = user.is_verified,
            Email = user.email,
            Birthday = user.birthday,
            Fullname = user.fullname,
            Country = user.country,
            Lastsession = user.last_session,
            Profilepic = user.profile_pic,
            Points = user.points

          }); ;

        }
        else // Unexistent user
        { 
          _logger.Log.Warning("[INFO] :: UserInfo :: User not found for ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User or email not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserInfo :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
            
    }

    [HttpPost("Favorites")]
    public async Task<IActionResult> Favorites([FromBody] idRequest userId, CancellationToken ct)
    {
      try
      {
        var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("app_sub") ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(userId.id) &&
            !string.Equals(userId.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          return Forbid();
        }

        var favorites = await _dbContext.Favorites
            .AsNoTracking()
            .Where(u => u.user_id == userId.id)
            .ToListAsync(ct);

        if (favorites.Count == 0)
        {
          _logger.Log.Warning("[INFO] :: Favorites :: Empty list of Favorites to userID: {msg}", userId.id);
          return NotFound(new { Message = "ERROR :: No Favorites!" });
        }

        var favsDTO = new List<FavoriteDTO>();

        foreach (var fav in favorites)
        {
          var tmpAsset = await _dbContext.FinancialAssets
              .AsNoTracking()
              .FirstOrDefaultAsync(fa => fa.ticker == fav.ticker, ct);

          if (tmpAsset == null) continue;

          var lastCandle = await _dbContext.AssetCandles
              .AsNoTracking()
              .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h")
              .OrderByDescending(c => c.DateTime)
              .FirstOrDefaultAsync(ct);

          if (lastCandle == null) continue;

          var lastDay = lastCandle.DateTime.Date;

          AssetCandle? prevCandle;

          if (tmpAsset.group == "Cryptos" || tmpAsset.group == "Forex")
          {
            prevCandle = await _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Skip(24)
                .FirstOrDefaultAsync(ct);
          }
          else
          {
            prevCandle = await _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h" && c.DateTime.Date < lastDay)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefaultAsync(ct);
          }

          double prevClose;
          double dailyGain;

          if (prevCandle != null)
          {
            prevClose = (double)prevCandle.Close;
            dailyGain = prevClose == 0 ? 0 : (((double)tmpAsset.current - prevClose) / prevClose) * 100.0;
          }
          else
          {
            prevClose = tmpAsset.current * 0.95;
            dailyGain = ((tmpAsset.current - prevClose) / prevClose) * 100.0;
          }

          favsDTO.Add(new FavoriteDTO(
              id: fav.id,
              name: tmpAsset.name,
              icon: tmpAsset.icon ?? "noIcon",
              daily_gain: dailyGain,
              close: prevClose,
              current: (double)lastCandle.Close,
              user_id: userId.id!,
              ticker: fav.ticker
          ));
        }

        _logger.Log.Debug("[INFO] :: Favorites :: success to ID: {msg}", userId.id);

        return Ok(new
        {
          Message = "Favorites SUCCESS",
          Favorites = favsDTO
        });
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[INFO] :: Favorites :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewFavorite")]
    public async Task<IActionResult> NewFavorite([FromBody] newFavoriteRequest newFavRequest)
    {
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(newFavRequest.user_id) &&
            !string.Equals(newFavRequest.user_id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

      }

      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: NewFavorite :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var assetts = _dbContext.FinancialAssets.ToList();
          var trends = _dbContext.Trends.ToList();
          var fav = _dbContext.Favorites.FirstOrDefault(fav => fav.user_id == newFavRequest.user_id && fav.ticker == newFavRequest.ticker);

          if (null != fav)
          {
            _dbContext.Favorites.Remove(fav);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { });
          }

          var newFavorite = new Favorite(id: Guid.NewGuid().ToString(), user_id: newFavRequest.user_id!, ticker: newFavRequest.ticker!);
          _dbContext.Favorites.Add(newFavorite);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();

          return Ok(new { });
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[INFO] :: NewFavorite :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("Trends")]
    public async Task<IActionResult> Trends([FromBody] idRequest userInfoRequest, CancellationToken ct)
    {
      try
      {
        var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("app_sub") ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(userInfoRequest.id) &&
            !string.Equals(userInfoRequest.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          return Forbid();
        }

        var trends = await _dbContext.Trends
            .AsNoTracking()
            .ToListAsync(ct);

        if (!trends.Any())
        {
          _logger.Log.Warning("[INFO] :: Trends :: Empty list of trends to ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "ERROR :: No trends!" });
        }

        var trendDTOs = new List<TrendDTO>();

        foreach (var trend in trends)
        {
          double prevClose;
          double dailyGain;

          var tmpAsset = await _dbContext.FinancialAssets
              .AsNoTracking()
              .FirstOrDefaultAsync(a => a.ticker == trend.ticker, ct);
                   

          var lastCandle = await _dbContext.AssetCandles
              .AsNoTracking()
              .Where(c => c.AssetId == tmpAsset!.id && c.Interval == "1h")
              .OrderByDescending(c => c.DateTime)
              .FirstOrDefaultAsync(ct);
                    

          if (lastCandle == null)
          {
            prevClose = tmpAsset!.current / ((100.0+trend.daily_gain)/100.0);
            
            trendDTOs.Add(new TrendDTO(
             id: trend.id,
             name: tmpAsset.name,
             icon: tmpAsset.icon ?? "noIcon",
             daily_gain: trend.daily_gain,
             close: prevClose,
             current: tmpAsset.current,
             ticker: trend.ticker));
            continue;
          }

          var lastDay = lastCandle.DateTime.Date;
          AssetCandle? finalCandle;
          

          if (tmpAsset!.group == "Cryptos" || tmpAsset.group == "Forex")
          {
            finalCandle = await _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h" && c.DateTime.Date == lastDay)
                .OrderBy(c => c.DateTime)
                .FirstOrDefaultAsync(ct);
          }
          else
          {
            finalCandle = lastCandle;
          }

          if (finalCandle == null)
          {
            prevClose = tmpAsset.current * 0.95;
            dailyGain = ((tmpAsset.current - prevClose) / prevClose) * 100.0;
            trendDTOs.Add(new TrendDTO(
             id: trend.id,
             name: tmpAsset.name,
             icon: tmpAsset.icon ?? "noIcon",
             daily_gain: dailyGain,
             close: prevClose,
             current: (double)finalCandle!.Close,
             ticker: trend.ticker
             ));
            continue;
          }

          var prevCandle = await _dbContext.AssetCandles
              .AsNoTracking()
              .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h" && c.DateTime.Date < lastDay)
              .OrderByDescending(c => c.DateTime)
              .FirstOrDefaultAsync(ct);

          

          if (prevCandle != null)
          {
            prevClose = (double)prevCandle.Close;
            dailyGain = prevClose == 0 ? 0 : (((double)finalCandle.Close - prevClose) / prevClose) * 100.0;
          }
          else
          {
            prevClose = tmpAsset.current * 0.95;
            dailyGain = ((tmpAsset.current - prevClose) / prevClose) * 100.0;
          }

          trendDTOs.Add(new TrendDTO(
              id: trend.id,
              name: tmpAsset.name,
              icon: tmpAsset.icon ?? "noIcon",
              daily_gain: dailyGain,
              close: prevClose,
              current: (double)finalCandle.Close,
              ticker: trend.ticker
          ));
        }

        _logger.Log.Debug("[INFO] :: Trends :: success with ID: {msg}", userInfoRequest.id);

        return Ok(new
        {
          Message = "Trends SUCCESS",
          Trends = trendDTOs
        });
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[INFO] :: Trends :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("TopUsers")]
    public IActionResult TopUsers([FromBody] idRequest userInfoRequest)
    {
      try
      {

        var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("app_sub") ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(userInfoRequest.id) &&
            !string.Equals(userInfoRequest.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var topUsers = _dbContext.Users.OrderByDescending(u => u.points).Take(50).ToList();

        if (topUsers.Any())
        {
          _logger.Log.Debug("[INFO] :: TopUsers :: success with user ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "TopUsers SUCCESS",
            Users = topUsers
          });
        }
        else // No users
        {
          _logger.Log.Warning("[INFO] :: TopUsers :: Empty list of users with user ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: TopUsers :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("TopUsersByCountry")]
    public IActionResult TopUsersByCountry([FromBody] idRequest countryCode)
    {
      try
      {
        var topUsersByCountry = _dbContext.Users.Where(u => u.country == countryCode.id).OrderByDescending(u => u.points).Take(50).ToList();

        if (topUsersByCountry.Any())
        {
          _logger.Log.Debug("[INFO] :: TopUsersByCountry :: success with country code: {msg}", countryCode.id);
          return Ok(new
          {
            Message = "TopUsersByCountry SUCCESS",
            Users = topUsersByCountry
          });
        }
        else // No users
        {
          _logger.Log.Warning("[INFO] :: TopUsersByCountry :: Empty list of users with country code: {msg}", countryCode.id);
          return NotFound(new { Message = "User has no bets!" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: TopUsersByCountry :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("UploadPic")]
    public async Task<IActionResult> UploadPic(uploadPicRequest uploadPicImageRequest)
    {
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(uploadPicImageRequest.id) &&
            !string.Equals(uploadPicImageRequest.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }
      }

      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UploadPic :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }


      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.id == uploadPicImageRequest.id);

          if (user != null && uploadPicImageRequest.Profilepic != "")
          {
            if (user.is_active && user.token_expiration > DateTime.UtcNow)
            {
              user.profile_pic = uploadPicImageRequest.Profilepic;
              await _dbContext.SaveChangesAsync();
              await transaction.CommitAsync();

              _logger.Log.Debug("[INFO] :: UploadPic :: Success on profile pic updating for ID: {msg}", uploadPicImageRequest.id);
              return Ok(new { Message = "Profile pic successfully updated!", UserId = user.id });
            }
            else
            {
              _logger.Log.Warning("[INFO] :: UploadPic :: No active session or session expired for ID: {msg}", uploadPicImageRequest.id);
              return BadRequest(new { Message = "No active session or session expired" });
            }
          }
          else
          {
            _logger.Log.Error("[INFO] :: UploadPic :: User token not found: {msg}", uploadPicImageRequest.id);
            return NotFound(new { Message = "User token not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[INFO] :: UploadPic :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("PendingBalance")]
    public IActionResult PendingBalance([FromBody] idRequest request)
    {
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });
        
        if (!string.IsNullOrEmpty(request.id) &&
            !string.Equals(request.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid(); 
        }

        var user = _dbContext.Users.FirstOrDefault(u => u.id == request.id);
        if (user == null)
          return NotFound(new { Message = "User not found" });


        if (user.password == "nullPassword" || user.password.Length < 12)
        {
          _logger.Log.Information("[INFO] :: PendingBalance :: Session active but password not set on id {id}", user.id);
          return StatusCode(StatusCodes.Status201Created, new { Message = "Password not set" });
        }

        return Ok(new { Balance = user.pending_balance });
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: PendingBalance :: Error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("PaymentHistory")]
    public async Task<IActionResult> PaymentHistory([FromBody] idRequest request)
    {
      if (request == null || request.id == String.Empty)
        return BadRequest(new { Message = "Invalid payload" });

      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.id) &&
            !string.Equals(request.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var exists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.id == request.id);
        if (!exists)
          return NotFound(new { Message = "User token not found" });

        var rows = await _dbContext.PaymentData
            .AsNoTracking()
            .Where(w => w.user_id == request.id)
            .OrderByDescending(w => w.executed_at)
            .ToListAsync();

        return Ok(rows.ToList());
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: PaymentHistory :: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("WithdrawalHistory")]
    public async Task<IActionResult> WithdrawalHistory([FromBody] idRequest request)
    {
      if (request == null || request.id == String.Empty)
        return BadRequest(new { Message = "Invalid payload" });

      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.id) &&
            !string.Equals(request.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var exists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.id == request.id);
        if (!exists)
          return NotFound(new { Message = "User token not found" });

        var rows = await _dbContext.WithdrawalData
            .AsNoTracking()
            .Where(w => w.user_id == request.id)
            .OrderByDescending(w => w.executed_at)
            .ToListAsync();

        return Ok(rows.ToList());
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: WithdrawalHistory :: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("StoreOptions")]
    public IActionResult StoreOptions([FromBody] storeOptionsrequest currencyandTypeRequest)
    {
      try
      {
        
        var path = Path.Combine(_env.ContentRootPath, $"exchange_options_{currencyandTypeRequest.currency}.json");

        if (!System.IO.File.Exists(path))
          return NotFound(new { Message = "Exchange options file not found" });

        
        var json = System.IO.File.ReadAllText(path);
        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);


        var filtered = parsed?
            .Where(item => item.ContainsKey("type") &&
                           item["type"]?.ToString()?.Equals(currencyandTypeRequest.type, StringComparison.OrdinalIgnoreCase) == true)
            .ToList() ?? new();

        return Ok(filtered);
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: Options :: Error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("RetireOptions")]
    public async Task<IActionResult> RetireOptions([FromBody] idRequest request)
    {
      if (request == null || request.id == String.Empty)
        return BadRequest(new { Message = "Invalid payload" });

      try
      {
        var exists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.id == request.id);
        if (!exists)
          return NotFound(new { Message = "User token not found" });

        var rows = await _dbContext.WithdrawalMethods
            .AsNoTracking()
            .Where(w => w.UserId == request.id)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
        
        var list = rows.Select(w => new WithdrawalMethodDto
        {
          Id = w.Id,
          Type = w.Type,
          Label = w.Label,
          Verified = w.Verified,
          Data = System.Text.Json.JsonSerializer.Deserialize<object>(
                w.Data.RootElement.GetRawText()
            )!,
          CreatedAt = w.CreatedAt,
          UpdatedAt = w.UpdatedAt
        }).ToList();

        return Ok(list);
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: RetireOptions :: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("DeleteRetireOption")]
    public async Task<IActionResult> DeleteRetireOption([FromBody] tokenRequest request)
    {
      if (request == null || request.user_id == String.Empty)
        return BadRequest(new { Message = "Invalid payload" });

      using var transaction = await _dbContext.Database.BeginTransactionAsync();

      try
      {
        var tokenUserId =
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("app_sub") ??
        User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.user_id) &&
            !string.Equals(request.user_id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var userExists = await _dbContext.Users.AsNoTracking().AnyAsync(u => u.id == request.user_id);
        if (!userExists) return NotFound(new { Message = "User token not found" });

        var currentRetireOption = await _dbContext.WithdrawalMethods.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == request.user_id && w.Label == request.token);
        if (currentRetireOption == null) return NotFound(new { Message = $"Retire option with label {request.token} not found for user {request.user_id}" });

        _dbContext.WithdrawalMethods.Remove(currentRetireOption);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.Log.Debug($"[INFO] :: DeleteRetireOption :: Retire option {request.token} removed successfully from user ID: {request.user_id}");
        return Ok(new { });
      }

      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: DeleteRetireOption :: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("AddBankRetireMethod")]
    public async Task<IActionResult> AddBankRetireMethod([FromBody] addBankWithdrawalMethodRequest request)
    {
      
      if (request == null) return BadRequest(new { Message = "Invalid payload" });

      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      
      try {

        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.UserId) &&
            !string.Equals(request.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.id == request.UserId);

        if (currentUser == null)
          return NotFound(new { Message = "User token not found" });
        
        var dataObj = new
        {
          iban = request.Iban?.Trim(),
          holder = request.Holder?.Trim(),
          bic = string.IsNullOrWhiteSpace(request.Bic) ? null : request.Bic.Trim()
        };
        var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(dataObj));

        var existing = await _dbContext.WithdrawalMethods
            .FirstOrDefaultAsync(w =>
                w.UserId == request.UserId &&
                w.Type == "bank" &&
                w.Label == request.Label);

        if (existing is null)
        {
          var entity = new WithdrawalMethod
          {
            UserId = request.UserId,
            Type = "bank",
            Label = request.Label!,
            Verified = currentUser.is_verified,
            Data = jsonDoc,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
          };

          _dbContext.WithdrawalMethods.Add(entity);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddBankRetireMethod :: Success on user : {id}", request.UserId);
          return Ok(new { id = entity.Id, created = true, verified = entity.Verified });
        }
        else
        {
          existing.Data = jsonDoc;
          existing.UpdatedAt = DateTime.UtcNow;
          _dbContext.WithdrawalMethods.Update(existing);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddBankRetireMethod :: Success on user : {id}", request.UserId);
          return Ok(new { id = existing.Id, created = false, verified = existing.Verified });
        }
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: AddBankRetireMethod :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("AddPaypalRetireMethod")]
    public async Task<IActionResult> AddPaypalRetireMethod([FromBody] addPaypalWithdrawalMethodRequest request)
    {
      if (request == null) return BadRequest(new { Message = "Invalid payload" });

      using var transaction = await _dbContext.Database.BeginTransactionAsync();

      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.UserId) &&
            !string.Equals(request.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.id == request.UserId);

        if (currentUser == null)
          return NotFound(new { Message = "User token not found" });

        var dataObj = new
        {
          email = request.Email?.Trim(),
        };
        var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(dataObj));

        var existing = await _dbContext.WithdrawalMethods
            .FirstOrDefaultAsync(w =>
                w.UserId == request.UserId &&
                w.Type == "paypal" &&
                w.Label == request.Label);

        if (existing is null)
        {
          var entity = new WithdrawalMethod
          {
            UserId = request.UserId,
            Type = "paypal",
            Label = request.Label!,
            Verified = currentUser.is_verified,
            Data = jsonDoc,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
          };

          _dbContext.WithdrawalMethods.Add(entity);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddPaypalRetireMethod :: Success on user : {id}", request.UserId);
          return Ok(new { id = entity.Id, created = true, verified = entity.Verified });
        }
        else
        {
          existing.Data = jsonDoc;
          existing.UpdatedAt = DateTime.UtcNow;
          _dbContext.WithdrawalMethods.Update(existing);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddPaypalRetireMethod  :: Success on user : {id}", request.UserId);
          return Ok(new { id = existing.Id, created = false, verified = existing.Verified });
        }
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: AddPaypalRetireMethod  :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }

    [HttpPost("AddCryptoRetireMethod")]
    public async Task<IActionResult> AddCryptoRetireMethod([FromBody] addCryptoWithdrawalMethodRequest request)
    {
      if (request == null) return BadRequest(new { Message = "Invalid payload" });

      using var transaction = await _dbContext.Database.BeginTransactionAsync();

      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(request.UserId) &&
            !string.Equals(request.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.id == request.UserId);

        if (currentUser == null)
          return NotFound(new { Message = "User token not found" });

        var dataObj = new
        {
          network = request.Network?.Trim(),
          address = request.Address?.Trim(),
          memo = request.Memo?.Trim(),

        };
        var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(dataObj));

        var existing = await _dbContext.WithdrawalMethods
            .FirstOrDefaultAsync(w =>
                w.UserId == request.UserId &&
                w.Type == "crypto" &&
                w.Label == request.Label);

        if (existing is null)
        {
          var entity = new WithdrawalMethod
          {
            UserId = request.UserId,
            Type = "crypto",
            Label = request.Label!,
            Verified = currentUser.is_verified,
            Data = jsonDoc,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
          };

          _dbContext.WithdrawalMethods.Add(entity);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddCryptoRetireMethod :: Success on user : {id}", request.UserId);
          return Ok(new { id = entity.Id, created = true, verified = entity.Verified });
        }
        else
        {
          existing.Data = jsonDoc;
          existing.UpdatedAt = DateTime.UtcNow;
          _dbContext.WithdrawalMethods.Update(existing);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: AddCryptoRetireMethod  :: Success on user : {id}", request.UserId);
          return Ok(new { id = existing.Id, created = false, verified = existing.Verified });
        }
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: AddCryptoRetireMethod  :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }
  }
}
