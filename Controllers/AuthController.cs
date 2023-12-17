using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Reflection;
using BetsTrading_Service.Database;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace BetsTrading_Service.Controllers
{

  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private const int SESSION_EXP_DAYS= 15;
    private readonly AppDbContext _dbContext;

    public AuthController(AppDbContext dbContext)
    {
      _dbContext = dbContext;
    }

    [HttpPost("LogIn")]
    public IActionResult LogIn([FromBody] LoginRequest loginRequest)
    {
      try
      {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.username == loginRequest.Username ||
                                 u.email == loginRequest.Username);

        if (user != null) // User exists, verify password
        {
          if (user.password == loginRequest.Password)
          {
            
            user.last_session = DateTime.UtcNow;
            user.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
            user.is_active = true;
            _dbContext.SaveChanges();

            return Ok(new { Message = "LogIn SUCCESS", UserId = user.id });
          }
          else
          {
            return BadRequest(new { Message = "Incorrect password. Try again" }); // Invalid password
          }
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

    [HttpPost("LogOut")]
    public IActionResult LogOut([FromBody] idRequest logOutRequest)
    {
      try
      {
        var user = _dbContext.Users.FirstOrDefault(u => u.id== logOutRequest.id);

        if (user != null) 
        {        
          user.last_session = DateTime.UtcNow;
          user.is_active = false;
          _dbContext.SaveChanges();
          return Ok(new { Message = "LogOut SUCCESS", UserId = user.id });
          
        }
        else
        {
          return NotFound(new { Message = "User or email not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
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
          return Ok(new { Message = "UserInfo SUCCESS", 
                          Username = user.username,
                          Email = user.email,
                          Birthday = user.birthday,
                          Fullname = user.fullname, 
                          Address = user.address,
                          Country = user.country,
                          Lastsession = user.last_session
          });
                    
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

    [HttpPost("IsLoggedIn")]
    public IActionResult IsLoggedIn(idRequest isLoggedRequest)
    {
      try
      {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == isLoggedRequest.id);

        if (user != null) 
        {
          if (user.is_active && user.token_expiration > DateTime.UtcNow)
          {
            user.last_session = DateTime.UtcNow;
            _dbContext.SaveChanges();

            return Ok(new { Message = "User is logged in", UserId = user.id }); 
          }
          else
          {
            return BadRequest(new { Message = "No active session or session expired" }); 
          }
        }
        else 
        {
          return NotFound(new { Message = "Token not found" }); 
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("SignIn")]
    public IActionResult SignIn([FromBody] SignUpRequest signUpRequest)
    {
      try
      {
        var existingUser = _dbContext.Users
            .FirstOrDefault(u => u.username == signUpRequest.Username || u.email == signUpRequest.Email || u.idcard== signUpRequest.IdCard);

        if (existingUser != null)
        {
          return Conflict(new { Message = "Username, email or ID already exists" }); 
        }

        // Crear un nuevo usuario
        var newUser = new User(
          Guid.NewGuid().ToString(),
          signUpRequest.IdCard ?? "nullIdCard",
          signUpRequest.FullName ?? "nullFullName",
          signUpRequest.Password ?? "nullPassword",
          signUpRequest.Address ?? "nullAddress",
          signUpRequest.Country ?? "nullCountry",
          signUpRequest.Gender ?? "nullGender",
          signUpRequest.Email ?? "nullEmail",
          signUpRequest.Birthday,
          DateTime.UtcNow, 
          DateTime.UtcNow, 
          signUpRequest.CreditCard ?? "nullCreditCard", 
          signUpRequest.Username ?? "nullUsername");
                
        newUser.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();

        return Ok(new { Message = "Registration succesfull!", UserId = newUser.id }); // SUCCESS
      }
      
      catch (Exception ex)
      {
          return StatusCode(500, new { Message = "Internal server error! ", Error = ex.Message });
      }
    }

  }

  public class LoginRequest
  {
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }
  }

  public class idRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 25)]
    public string? id { get; set; }

    
  }

  public class SignUpRequest
  {
    [Required]
    public string? IdCard { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? FullName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    public string? Address { get; set; }

    [Required]
    public string? Country { get; set; }

    public string? Gender { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public DateTime Birthday { get; set; }

    // REMOVE FROM HERE IN FUTURE
    public string? CreditCard { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }
  }


}

