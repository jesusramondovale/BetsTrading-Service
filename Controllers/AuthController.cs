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

    [HttpPost("SignIn")]
    public async Task<IActionResult> SignIn([FromBody] SignUpRequest signUpRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var existingUser = _dbContext.Users
              .FirstOrDefault(u => u.username == signUpRequest.Username || u.email == signUpRequest.Email);

          if (existingUser != null)
          {
            return Conflict(new { Message = "Username, email or ID already exists" });
          }
          
          string hashedPassword = BCrypt.Net.BCrypt.HashPassword(signUpRequest.Password);
          
          var newUser = new User(
              signUpRequest.Token ?? Guid.NewGuid().ToString(),
              signUpRequest.IdCard ?? "-",
              signUpRequest.Fcm ?? "-",
              signUpRequest.FullName ?? "-",
              hashedPassword,
              signUpRequest.Country ?? "",
              signUpRequest.Gender ?? "-",
              signUpRequest.Email ?? "-",
              signUpRequest.Birthday ?? DateTime.UtcNow,
              DateTime.UtcNow,
              DateTime.UtcNow,
              signUpRequest.CreditCard ?? "nullCreditCard",
              signUpRequest.Username ?? "ERROR",
              signUpRequest.ProfilePic ?? null!,
              0);

          newUser.is_active = true;
          newUser.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);

          _dbContext.Users.Add(newUser);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();

          _logger.Log.Information("[AUTH] :: SignIn :: Success with user ID : {userID}", newUser.id);
          return Ok(new { Message = "Registration successful!", UserId = newUser.id });
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: SignIn :: Error : {msg}", ex.Message);
          return StatusCode(500, new { Message = "Internal server error!", Error = ex.Message });
        }
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
      signUpRequest.Fcm = isGoogledRequest.fcm;
      signUpRequest.Email = isGoogledRequest.email;
      signUpRequest.ProfilePic = isGoogledRequest.photoUrl;
      signUpRequest.Birthday = isGoogledRequest.birthday;
      signUpRequest.Country = isGoogledRequest.country;
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

    [HttpPost("LogIn")]
    public async Task<IActionResult> LogIn([FromBody] Requests.LoginRequest loginRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users
              .FirstOrDefault(u => u.username == loginRequest.Username || u.email == loginRequest.Username);

          if (user != null)
          {
           
            if (BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.password))
            {
              user.last_session = DateTime.UtcNow;
              user.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
              user.is_active = true;
              await _dbContext.SaveChangesAsync();
              await transaction.CommitAsync();

              _logger.Log.Information("[AUTH] :: LogIn :: Success. User ID: {userId}", user.id);
              return Ok(new { Message = "LogIn SUCCESS", UserId = user.id });
            }
            else
            {
              return BadRequest(new { Message = "incorrectPassword" });
            }
          }
          else
          {
            return NotFound(new { Message = "userOrEmailNotFound" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: LogIn :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] Requests.LoginRequest changepasswordRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users
              .FirstOrDefault(u => u.id == changepasswordRequest.Username);

          if (user != null)
          {            
            if (string.IsNullOrWhiteSpace(changepasswordRequest.Password))
            {
              return BadRequest(new { Message = "Password cannot be empty" });
            }
           
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(changepasswordRequest.Password);
            user.password = hashedPassword;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { Message = "Password updated successfully" });

          }
          else
          {
            return NotFound(new { Message = "User not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: ChangePassword :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
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
    public async Task<IActionResult> LogOut([FromBody] idRequest logOutRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.id == logOutRequest.id);

          if (user != null)
          {
            user.last_session = DateTime.UtcNow;
            user.is_active = false;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[AUTH] :: LogOut :: Success on user {username}", user.username);
            return Ok(new { Message = "LogOut SUCCESS", UserId = user.id });
          }
          else
          {
            return NotFound(new { Message = "User or email not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: LogOut :: Error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
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

    [HttpPost("RefreshFCM")]
    public async Task<IActionResult> RefreshFCM([FromBody] fcmTokenRequest tokenRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.id == tokenRequest.user_id);

          if (user != null)
          {
            user.fcm = tokenRequest.fcm_token!;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[AUTH] :: RefreshFCM :: Success with User ID {id}", tokenRequest.user_id);
            return Ok(new { Message = "FCM token updated successfully", UserId = user.id });
          }
          else
          {
            return NotFound(new { Message = "User not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: RefreshFCM :: Internal Server Error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("VerifyID")]
    public async Task<IActionResult> Verify([FromBody] idCardRequest idCardRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.id == idCardRequest.id);

          if (user != null)
          {
            user.idcard = idCardRequest.idCard!;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[AUTH] :: Verify :: Success with ID Card {idCard}", idCardRequest.idCard);
            return Ok(new { Message = "ID card updated successfully", UserId = user.id });
          }
          else
          {
            return NotFound(new { Message = "User not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: Verify :: Internal Server Error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }



  }
}

