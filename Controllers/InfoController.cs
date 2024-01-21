using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using BetsTrading_Service.Database;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class InfoController : ControllerBase
  {
    private readonly AppDbContext _dbContext;
   

    public InfoController(AppDbContext dbContext)
    {
      _dbContext = dbContext;
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
          return NotFound(new { Message = "User or email not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
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
          return Ok(new
          {
            Message = "UserBets SUCCESS",
            Bets = bets

          }); ;

        }
        else // No bets user
        {
          return NotFound(new { Message = "User has no bets!" }); // User not found
        }
      }
      catch (Exception ex)
      {
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

            return Ok(new { Message = "Profile pic succesfully updated!", UserId = user.id });
          }
          else
          {
            return BadRequest(new { Message = "No active session or session expired" });
          }
        }
        else
        {
          return NotFound(new { Message = "User token not found" });
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }


  }
}
