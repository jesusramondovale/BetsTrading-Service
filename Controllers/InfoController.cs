using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
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
            Email = user.email,
            Birthday = user.birthday,
            Fullname = user.fullname,
            Address = user.address,
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
            Message = "UserBets SUCCESS",
            Trends = trends

          }); ;

        }
        else // No trends
        {
          _logger.Log.Warning("[INFO] :: UserBets :: Empty list of bets to userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "EROR :: No trends!" }); 
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }


    [HttpPost("UserBets")]
    public IActionResult UserBets([FromBody] idRequest userInfoRequest)
    {
      

      try
      {
        var bets = _dbContext.InvestmentData
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
