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
  public class AuthController : ControllerBase
  {
    private const int SESSION_EXP_DAYS= 15;
    private readonly AppDbContext _dbContext;

    public AuthController(AppDbContext dbContext)
    {
      _dbContext = dbContext;
    }

    [HttpPost("LogIn")]
    public IActionResult LogIn([FromBody] Requests.LoginRequest loginRequest)
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

    [HttpPost("GoogleLogIn")]
    public IActionResult GoogleLogIn([FromBody] Requests.LoginRequest loginRequest)
    {
      try
      {
        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == loginRequest.Username);

        if (user != null) // Google-User exists
        {
          
          user.last_session = DateTime.UtcNow;
          user.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
          user.is_active = true;
          _dbContext.SaveChanges();

          return Ok(new { Message = "Google LogIn SUCCESS", UserId = user.id });        
          
        }
        else // Unexistent user
        {
          return NotFound(new { Message = "User found" }); // User not found
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
      Console.WriteLine("SignIn() called");
      Console.WriteLine("Requesst -> " + signUpRequest.FullName);
      try
      {
        var existingUser = _dbContext.Users
            .FirstOrDefault(u => u.username == signUpRequest.Username || u.email == signUpRequest.Email);

        if (existingUser != null)
        {
          Console.WriteLine("Conflict!");
          return Conflict(new { Message = "Username, email or ID already exists" }); 
        }

        Console.WriteLine("OK1");
        var newUser = new User(
          signUpRequest.Token ?? Guid.NewGuid().ToString(),
          signUpRequest.IdCard ?? "-",
          signUpRequest.FullName ?? "-",
          signUpRequest.Password ?? "-",
          signUpRequest.Address ?? "-",
          signUpRequest.Country ?? "",
          signUpRequest.Gender ?? "-",
          signUpRequest.Email ?? "-",
          signUpRequest.Birthday ?? DateTime.UtcNow,
          DateTime.UtcNow,
          DateTime.UtcNow,
          signUpRequest.CreditCard ?? "nullCreditCard",
          signUpRequest.Username ?? "ERROR",
          signUpRequest.ProfilePic ?? null);
          newUser.is_active = true;
          newUser.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
        
        Console.WriteLine("OK2");
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();

        Console.WriteLine("SignIn() OK)");
        return Ok(new { Message = "Registration succesfull!", UserId = newUser.id }); // SUCCESS
                      
      }
      
      catch (Exception ex)
      {
          return StatusCode(500, new { Message = "Internal server error! ", Error = ex.Message });
      }
    }

    [HttpPost("GoogleQuickRegister")]
    public IActionResult GoogleQuickRegister(googleSignRequest isGoogledRequest)
    {
      Console.WriteLine("GoogleQuickRegister() called");
      SignUpRequest signUpRequest = new SignUpRequest();
      signUpRequest.Token = isGoogledRequest.id;
      if (!string.IsNullOrEmpty(isGoogledRequest.email))
      {
        signUpRequest.Username = isGoogledRequest.email.Split('@')[0];
      }
      else
      {
        signUpRequest.Username = isGoogledRequest.displayName;
      }

      signUpRequest.FullName = isGoogledRequest.displayName;
      signUpRequest.Email= isGoogledRequest.email;
      signUpRequest.ProfilePic = isGoogledRequest.photoUrl;
      signUpRequest.Birthday = isGoogledRequest.birthday;
      var signInResult = SignIn(signUpRequest);

      
      if (signInResult is OkObjectResult)
      {
        Console.WriteLine("SignIn external() OK");
        return Ok(new { Message = "User quick-registered", UserId = signUpRequest.Token });
      }
      else
      {
        Console.WriteLine("SignIn() INTERNAL SERVER ERROR");
        return StatusCode(500, new { Message = "Internal server error! ", Error = "Internal server error  while google quick-regist" });
      }
     
    }

  }

}

