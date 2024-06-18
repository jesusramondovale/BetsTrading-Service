using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;


namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private const int SESSION_EXP_DAYS= 15;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public AuthController(AppDbContext dbContext, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _logger = customLogger;      
     
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
            _logger.Log.Information("[AUTH] :: LogIn :: Sucess. User ID: {userId}", user.id);
            return Ok(new { Message = "LogIn SUCCESS", UserId = user.id });
          }
          else
          {
            _logger.Log.Warning("[AUTH] :: LogIn ::BadRequest on LogIn: Bad pass");
            return BadRequest(new { Message = "Incorrect password. Try again" }); // Invalid password
          }
        }
        else // Unexistent user
        {
          _logger.Log.Warning("[AUTH] :: LogIn :: User not found  with username: {logInRequest}", loginRequest.Username);
          return NotFound(new { Message = "User or email not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[AUTH] :: LogIn :: InternaStatusCode on LogIn with request: {logInRequest}", loginRequest.ToString());
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
          _logger.Log.Information("[AUTH] :: Google LogIn :: Sucess. User ID: {userId}", user.id);
          return Ok(new { Message = "Google LogIn SUCCESS", UserId = user.id });

        }
        else // Unexistent user
        {
          _logger.Log.Warning("[AUTH] :: Google LogIn :: Unexistent user : {userRequest} ", loginRequest.Username);
          return NotFound(new { Message = "User found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[AUTH] :: Google LogIn :: Internal server error : {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }     
    }

    [HttpPost("LogOut")]
    public IActionResult LogOut([FromBody] idRequest logOutRequest)
    {    
      try
      {
        var user = _dbContext.Users.FirstOrDefault(u => u.id == logOutRequest.id);

        if (user != null)
        {
          user.last_session = DateTime.UtcNow;
          user.is_active = false;
          _dbContext.SaveChanges();
          _logger.Log.Information("[AUTH] :: LogOut :: Success on user {username}", user.username);
          return Ok(new { Message = "LogOut SUCCESS", UserId = user.id });

        }
        else
        {
          _logger.Log.Error("[AUTH] :: LogOut :: Not found. ID {id}", logOutRequest.id);
          return NotFound(new { Message = "User or email not found" }); // User not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[AUTH] :: LogOut :: Error : {ex.Message}", ex.Message);
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
            _logger.Log.Information("[AUTH] :: IsLoggedIn :: Session active on id {id}", isLoggedRequest.id);
            return Ok(new { Message = "User is logged in", UserId = user.id });
          }
          else
          {
            _logger.Log.Warning("[AUTH] :: IsLoggedIn :: Session inactive or expired on id {id}", isLoggedRequest.id);
            return BadRequest(new { Message = "No active session or session expired" });
          }
        }
        else
        {
          _logger.Log.Warning("[AUTH] :: IsLoggedIn :: Token not found : {id}", isLoggedRequest.id);
          return NotFound(new { Message = "Token not found" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[AUTH] :: IsLoggedIn :: Internal server error : {msg}", ex.Message);
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
          signUpRequest.ProfilePic ?? null!);

        newUser.is_active = true;
        newUser.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);

        _dbContext.Users.Add(newUser);
        _dbContext.SaveChanges();

        _logger.Log.Information("[AUTH] :: SignIn :: Success with user ID : {userID}", newUser.id);
        return Ok(new { Message = "Registration succesfull!", UserId = newUser.id }); // SUCCESS

      }

      catch (Exception ex)
      {
        _logger.Log.Error("[AUTH] :: SignIn :: Error : {msg}", ex.Message);
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
      signUpRequest.Email = isGoogledRequest.email;
      signUpRequest.ProfilePic = isGoogledRequest.photoUrl;
      signUpRequest.Birthday = isGoogledRequest.birthday;
      var signInResult = SignIn(signUpRequest);


      if (signInResult is OkObjectResult)
      {
        Console.WriteLine("SignIn external() OK");
        _logger.Log.Information("[AUTH] :: GoogleQuickRegister :: Success with token : {token}", signUpRequest.Token);
        return Ok(new { Message = "User quick-registered", UserId = signUpRequest.Token });
      }
      else
      {
        _logger.Log.Error("[AUTH] :: GoogleQuickRegister :: Error: Internal SignIn error!");
        Console.WriteLine("SignIn() INTERNAL SERVER ERROR");
        return StatusCode(500, new { Message = "Internal server error! ", Error = "Internal server error  while google quick-regist" });
      }

    }   
  }
}

