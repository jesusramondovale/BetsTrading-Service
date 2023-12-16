using System;
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

        if (user != null) // User exists, verify pass
        {        
          if (user.password == loginRequest.Password) 
          {        
            return Ok(new { Message = "LogIn SUCCESS", UserId = user.id }); // Success
          }
          else 
          {
            return BadRequest(new { Message = "Incorrect password. Try again" }); // Invalid pass
          }
        }
        else // Unexistent user
        {
          return NotFound(new { Message = "User or mail not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { Message = "Error en el servidor", Error = ex.Message });
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
            signUpRequest.IdCard,
            signUpRequest.FullName,
            signUpRequest.Password,
            signUpRequest.Address,
            signUpRequest.Country,
            signUpRequest.Gender,
            signUpRequest.Email,
            signUpRequest.Birthday,
            DateTime.UtcNow,
            DateTime.UtcNow,
            signUpRequest.CreditCard,
            signUpRequest.Username);
      
        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();

        return Ok(new { Message = "Registration succesfull!", UserId = newUser.id }); // Success
      }
      
      catch (Exception ex)
      {
          return StatusCode(500, new { Message = "Internal server error! ", Error = ex.Message });
      }
    }

  }

  public class LoginRequest
  {
    public string? Username { get; set; }
    public string? Password { get; set; }
  }

  public class SignUpRequest
  {
    public string? IdCard { get; set; }
    public string? FullName { get; set; }
    public string? Password { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public DateTime Birthday { get; set; }
    public string? CreditCard { get; set; }
    public string? Username { get; set; }
  }

}

