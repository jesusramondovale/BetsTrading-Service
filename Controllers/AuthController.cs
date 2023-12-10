using System;
using System.Linq;
using BetsTrading_Service.Database;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;


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



  }

  public class LoginRequest
  {
    public string? Username { get; set; }
    public string? Password { get; set; }
  }


}

