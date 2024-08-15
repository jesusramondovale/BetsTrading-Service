using BetsTrading_Service.Database;
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
            Profilepic = user.profile_pic
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
    public IActionResult Favorites([FromBody] idRequest favoritesRequest)
    {

      try
      {

        var favorites = _dbContext.Favorites.Where(u => u.user_id == favoritesRequest.id).ToList();

        if (favorites != null && favorites.Count != 0) // There are favorites
        {
          _logger.Log.Information("[INFO] :: Favorites :: success to ID: {msg}", favoritesRequest.id);
          return Ok(new
          {
            Message = "Favorites SUCCESS",
            Favorites = favorites

          }); ;

        }
        else // No Favorites
        {
          _logger.Log.Warning("[INFO] :: Favorites :: Empty list of Favorites to userID: {msg}", favoritesRequest.id);
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
    public IActionResult NewFavorite([FromBody] newFavoriteRequest newFavRequest)
    {
      try
      {
        var assetts = _dbContext.Trends.ToList();
        var fav = _dbContext.Favorites.FirstOrDefault(u => u.user_id == newFavRequest.id && u.name == newFavRequest.item_name);

        if (null != fav)
        {
          _dbContext.Favorites.Remove(fav);
          _dbContext.SaveChanges();
          return Ok(new { });

        }
        
        var tmpAsset = assetts.FirstOrDefault(a => a.name == newFavRequest.item_name);

        if (tmpAsset == null)
        {
          return NotFound(new { Message = "Asset not found" });
        }

        
        var favoriteId = Guid.NewGuid().ToString();

        var newFavorite = new Favorite(
            favoriteId,
            newFavRequest.item_name!,
            tmpAsset.icon,
            tmpAsset.daily_gain,
            tmpAsset.close,
            tmpAsset.current,
            newFavRequest.id!
        );

        _dbContext.Favorites.Add(newFavorite);
        _dbContext.SaveChanges();

        return Ok(new { });
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: New favorite :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("DeleteRecentBet")]
    public IActionResult DeleteRecentBet([FromBody] idRequest betIdRequest)
    {

      try
      {
        var bet = _dbContext.Bet
            .FirstOrDefault(u => u.id.ToString() == betIdRequest.id);

        if (bet != null) // Bet exists
        {
          _dbContext.Bet.Remove(bet);
          _dbContext.SaveChanges();
          _logger.Log.Warning("[INFO] :: DeleteRecentBet :: Bet removed succesfuly with ID: {msg}", betIdRequest.id);
          return Ok(new { });

        }

        else // Bet not found
        {
          _logger.Log.Warning("[INFO] :: DeleteRecentBet :: Bet not found for ID: {msg}", betIdRequest.id);
          return NotFound(new { Message = "User or email not found" }); 
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserInfo :: DeleteRecentBet server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }



    }

    [HttpPost("Trends")]
    public IActionResult Trends([FromBody] idRequest userInfoRequest)
    {

      try
      {
        /* TO-DO : Custom trends by user
        var trends = _dbContext.Trends.Where(u => u.user_id == userInfoRequest.id)ToList();
        */

        var trends = _dbContext.Trends.ToList();

        if (trends != null && trends.Count != 0) // There are trends
        {
          _logger.Log.Information("[INFO] :: Trends :: success with ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "Trends SUCCESS",
            Trends = trends

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


    [HttpPost("UserBets")]
    public IActionResult UserBets([FromBody] idRequest userInfoRequest)
    {
      

      try
      {
        var bets = _dbContext.Bet
            .Where(u => u.user_id == userInfoRequest.id).ToList();

        if (bets != null && bets.Count != 0) // There are bets
        {
          _logger.Log.Information("[INFO] :: UserBets :: success with ID: {msg}", userInfoRequest.id);
          return Ok(new
          {
            Message = "UserBets SUCCESS",
            Bets = bets

          }); ;

        }
        else // No bets user
        {
          _logger.Log.Warning("[INFO] :: UserBets :: Empty list of bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" }); // User not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

      
     
    }


    [HttpPost("UploadPic")]
    public IActionResult UploadPic(uploadPicRequest uploadPicImageRequest)
    {
      

      try
      {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == uploadPicImageRequest.id);

        if (user != null && uploadPicImageRequest.Profilepic != "")
        {
          if (user.is_active && user.token_expiration > DateTime.UtcNow)
          {
            user.profile_pic = uploadPicImageRequest.Profilepic;
            _dbContext.SaveChanges();
            _logger.Log.Information("[INFO] :: UploadPic :: Success on profile pic updating for ID: {msg}", uploadPicImageRequest.id);
            return Ok(new { Message = "Profile pic succesfully updated!", UserId = user.id });
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
        _logger.Log.Error("[INFO] :: UploadPic :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

      
      
    }

  }
}
