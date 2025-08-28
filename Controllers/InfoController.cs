using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Text.Json;

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
        

    [HttpPost("UserInfo")]
    public IActionResult UserInfo([FromBody] idRequest userInfoRequest)
    {
     
      try
      {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == userInfoRequest.id);

        if (user != null) // User exists
        {
          
          _logger.Log.Information("[INFO] :: UserInfo :: Success on ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "UserInfo SUCCESS",
            Username = user.username,
            Idcard = user.idcard,
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
    public IActionResult Favorites([FromBody] idRequest userId)
    {
      try
      {
        var favorites = _dbContext.Favorites
            .AsNoTracking()
            .Where(u => u.user_id == userId.id)
            .ToList();

        if (favorites == null || favorites.Count == 0)
        {
          _logger.Log.Warning("[INFO] :: Favorites :: Empty list of Favorites to userID: {msg}", userId.id);
          return NotFound(new { Message = "ERROR :: No Favorites!" });
        }

        var favsDTO = new List<FavoriteDTO>();

        foreach (var fav in favorites)
        {
          var tmpAsset = _dbContext.FinancialAssets
              .AsNoTracking()
              .FirstOrDefault(fa => fa.ticker == fav.ticker);

          // Comprobar si hay datos y que close tenga al menos 2 elementos
          if (tmpAsset == null || tmpAsset.close == null)
            continue;

          var dailyGain = 0.0;
          var prevClose = 0.0;
          
          if (tmpAsset.close.Count >= 2)
          {
            prevClose = tmpAsset.close[1];
            dailyGain = ((tmpAsset.current - prevClose) / prevClose) * 100;
          }
          else
          {
            //Fake increment 5% on assets with no data
            prevClose = tmpAsset.current * 0.95;
            dailyGain = ((tmpAsset.current - prevClose) / prevClose) * 100;
          }

          favsDTO.Add(new FavoriteDTO(
              id: fav.id,
              name: tmpAsset.name,
              icon: tmpAsset.icon ?? "noIcon",
              daily_gain: dailyGain,
              close: prevClose,
              current: tmpAsset.current,
              user_id: userId.id!,
              ticker: fav.ticker
          ));
        }

        _logger.Log.Information("[INFO] :: Favorites :: success to ID: {msg}", userId.id);

        return Ok(new
        {
          Message = "Favorites SUCCESS",
          Favorites = favsDTO
        });
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: Favorites :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewFavorite")]
    public async Task<IActionResult> NewFavorite([FromBody] newFavoriteRequest newFavRequest)
    {
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
    public IActionResult Trends([FromBody] idRequest userInfoRequest)
    {

      try
      {
        /* TO-DO : Custom trends by user*/

        var trends = _dbContext.Trends.ToList();

        if (trends.Any())
        {
          List<TrendDTO> trendDTOs = new List<TrendDTO>();
          foreach (var trend in trends) {

            var tmpAsset = _dbContext.FinancialAssets.FirstOrDefault(a => a.ticker == trend.ticker);
            if (tmpAsset != null)
            {
              if (tmpAsset.close.Count == 1)
              {
                double dailyGain = ((tmpAsset.current - tmpAsset.close[0]) / tmpAsset.close[0]) * 100;
                trendDTOs.Add(new TrendDTO(id: trend.id, name: tmpAsset.name, icon: tmpAsset.icon!, daily_gain: dailyGain,
                  close: tmpAsset.close[0], current: tmpAsset.current, ticker: trend.ticker));
              }
              else
              {
                double dailyGain = ((tmpAsset.current - tmpAsset.close[1]) / tmpAsset.close[1]) * 100;
                trendDTOs.Add(new TrendDTO(id: trend.id, name: tmpAsset.name, icon: tmpAsset.icon!, daily_gain: dailyGain,
                  close: tmpAsset.close[1], current: tmpAsset.current, ticker: trend.ticker));
              }
            }
            
            else
            {
              trendDTOs.Add(new TrendDTO(id: trend.id, name: "?", icon: "null", daily_gain: trend.daily_gain, close: 0.0, current: 0.0, ticker: trend.ticker));
            }
            
          }
          _logger.Log.Information("[INFO] :: Trends :: success with ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "Trends SUCCESS",
            Trends = trendDTOs

          }); ;

        }
        else // No trends
        {
          _logger.Log.Warning("[INFO] :: Trends :: Empty list of trends to ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "ERROR :: No trends!" }); 
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: Trends :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }

    [HttpPost("TopUsers")]
    public IActionResult TopUsers([FromBody] idRequest userInfoRequest)
    {
      try
      {
        var topUsers = _dbContext.Users.OrderByDescending(u => u.points).ToList();


        if (topUsers.Any())
        {
          _logger.Log.Information("[INFO] :: TopUsers :: success with user ID: {msg}", userInfoRequest.id);
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
        var topUsersByCountry = _dbContext.Users.Where(u => u.country == countryCode.id).OrderByDescending(u => u.points).ToList();


        if (topUsersByCountry.Any())
        {
          _logger.Log.Information("[INFO] :: TopUsersByCountry :: success with country code: {msg}", countryCode.id);
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

              _logger.Log.Information("[INFO] :: UploadPic :: Success on profile pic updating for ID: {msg}", uploadPicImageRequest.id);
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
            Verified = !(currentUser.idcard.Length < 5), // marca verificado si tiene idcard
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
            Verified = !(currentUser.idcard.Length < 5),
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
            Verified = !(currentUser.idcard.Length < 5),
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
