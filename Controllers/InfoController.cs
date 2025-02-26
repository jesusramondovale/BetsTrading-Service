﻿using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class InfoController : ControllerBase
  {
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public InfoController(AppDbContext dbContext, ICustomLogger customLogger)
    {
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

        var favorites = _dbContext.Favorites.Where(u => u.user_id == userId.id).ToList();

        if (favorites != null && favorites.Count != 0) // There are favorites
        {
          List<FavoriteDTO> favsDTO = new List<FavoriteDTO>();
          foreach (var fav in favorites)
          {
            var tmpAsset = _dbContext.FinancialAssets.Where(fa => fa.ticker == fav.ticker).FirstOrDefault();
            
            favsDTO.Add(new FavoriteDTO(id: fav.id, name: tmpAsset!.name, icon: tmpAsset.icon!, 
              daily_gain: ((tmpAsset.current - tmpAsset.close[1]) / tmpAsset.close[1])*100, close: tmpAsset.close[1], current: tmpAsset.current, user_id: userId.id!, ticker: fav.ticker));
          }

          _logger.Log.Information("[INFO] :: Favorites :: success to ID: {msg}", userId.id);
          return Ok( new
          {
            Message = "Favorites SUCCESS",
            Favorites = favsDTO

          }); ;

        }
        else // No Favorites
        {
          _logger.Log.Warning("[INFO] :: Favorites :: Empty list of Favorites to userID: {msg}", userId.id);
          return NotFound(new { Message = "ERROR :: No Favorites!" });
        }
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

  }
}
