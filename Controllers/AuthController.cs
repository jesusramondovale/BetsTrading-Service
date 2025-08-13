using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using BetsTrading_Service.Services;
using BetsTrading_Service.Locale;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private const int SESSION_EXP_DAYS= 15;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    private readonly FirebaseNotificationService _firebaseNotificationService;

    public AuthController(AppDbContext dbContext, ICustomLogger customLogger, FirebaseNotificationService firebaseNotificationService)
    {
      _dbContext = dbContext;
      _logger = customLogger;      
      _firebaseNotificationService = firebaseNotificationService;
    }

    [HttpPost("SignIn")]
    public Task<IActionResult> SignIn([FromBody] SignUpRequest req)
    {
      return RegisterInternal(req);
    }


    private async Task<IActionResult> RegisterInternal(SignUpRequest signUpRequest)
    {
      var transaction = await _dbContext.Database.BeginTransactionAsync();
      {
        try
        {
          var existingUser = await _dbContext.Users
              .FirstOrDefaultAsync(u => u.username == signUpRequest.Username || u.email == signUpRequest.Email);

          if (existingUser != null)
          {
            return Conflict(new { Message = "Username, email or ID already exists" });
          }

          string hashedPassword = (signUpRequest.Password != null ? BCrypt.Net.BCrypt.HashPassword(signUpRequest.Password) : "nullPassword");

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
    public async Task<IActionResult> GoogleQuickRegister(googleSignRequest isGoogledRequest)
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
      var signInResult = await RegisterInternal(signUpRequest);


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
          string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
          var geo = await GetGeoLocationFromIp(ip!);

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
              _logger.Log.Information("[AUTH] :: LogIn :: Success. User ID: {userId} FROM {city} , {region} , {country} , ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country, geo.ISP);
              return Ok(new { Message = "LogIn SUCCESS", UserId = user.id });
            }
            else
            {
                            
              if (geo == null)
              {
                _logger.Log.Error("[AUTH] :: INCORRECT LOGIN ATTEMPT FOR USER {user} FROM IP {ip}", loginRequest.Username, ip ?? "UNKNOWN");
              }
              else
              {
                _logger.Log.Error("[AUTH] :: INCORRECT LOGIN ATTEMPT FOR USER {user} FROM IP {ip} -> {city} ,{region} ,{country} , ISP: {isp}",
                loginRequest.Username, ip ?? "UNKNOWN", geo.City, geo.RegionName, geo.Country, geo.ISP);
              }
              
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

    [HttpPost("NewPassword")]
    public async Task<IActionResult> NewPassword([FromBody] Requests.LoginRequest newPasswordRequest)
    {

      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
          var geo = await GetGeoLocationFromIp(ip!);

          var user = _dbContext.Users.FirstOrDefault(u => u.id == newPasswordRequest.Username);
          if (user == null) return NotFound(new { Message = "User not found" });
     

          string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPasswordRequest.Password);
          user.password = hashedPassword;
          _dbContext.Users.Update(user);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Information("[AUTH] :: NewPassword :: Success. User ID: {userId} FROM -> {city} , {region} , {country} , ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country, geo.ISP);
          return Ok(new { Message = "Password created successfully" });

          
          
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: ChangePassword :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] Requests.ChangePasswordRequest changepasswordRequest)
    {
      if (changepasswordRequest.Current == changepasswordRequest.Password) return BadRequest(new { Message = "New password matches current password" });

      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
          var geo = await GetGeoLocationFromIp(ip!);

          var user = _dbContext.Users.FirstOrDefault(u => u.id == changepasswordRequest.Username);
          if (user == null) return NotFound(new { Message = "User not found" });

          if (BCrypt.Net.BCrypt.Verify(changepasswordRequest.Current, user.password))
          {            
            if (string.IsNullOrWhiteSpace(changepasswordRequest.Password) || changepasswordRequest.Password.Length < 12)
            {
              return BadRequest(new { Message = "Bad new password" });
            }
           
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(changepasswordRequest.Password);
            user.password = hashedPassword;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.Log.Information("[AUTH] :: ChanePassword :: Success. User ID: {userId} FROM -> {city} , {region} , {country} , ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country, geo.ISP);
            return Ok(new { Message = "Password updated successfully" });

          }
          else
          {

            if (geo == null)
            {
              _logger.Log.Error("[AUTH] :: INCORRECT CHANGE PASSWORD ATTEMPT FOR USER {user} FROM IP {ip}", changepasswordRequest.Username, ip ?? "UNKNOWN");
            }
            else
            {
              _logger.Log.Error("[AUTH] :: INCORRECT CHANGE PASSWORD ATTEMPT FOR USER {user} FROM IP {ip} -> {city} ,{region} ,{country} , ISP: {isp}",
               changepasswordRequest.Username, ip ?? "UNKNOWN", geo.City, geo.RegionName, geo.Country, geo.ISP);
            }

            return BadRequest(new { Message = "incorrectPassword" });
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
    public async Task<IActionResult> GoogleLogIn([FromBody] Requests.LoginRequest loginRequest)
    {    
      try
      {
        string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
        var geo = await GetGeoLocationFromIp(ip!);
        var user = _dbContext.Users
            .FirstOrDefault(u => u.id == loginRequest.Username);

        if (user != null) // Google-User exists
        {

          user.last_session = DateTime.UtcNow;
          user.token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS);
          user.is_active = true;
          _dbContext.SaveChanges();
          _logger.Log.Information("[AUTH] :: Google LogIn :: Sucess. User ID: {userId} from {city} , {region} , {country}. ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country , geo.ISP);
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

          if (user.password == "nullPassword" || user.password.Length == 0)
          {
            _logger.Log.Information("[AUTH] :: IsLoggedIn :: Session active but password not set on id {id}", isLoggedRequest.id);
            return StatusCode(StatusCodes.Status201Created, new { Message = "Password not set" });
          }

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
    public async Task<IActionResult> RefreshFCM([FromBody] tokenRequest tokenRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {

          string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
          var geo = await GetGeoLocationFromIp(ip!);
          var user = _dbContext.Users.FirstOrDefault(u => u.id == tokenRequest.user_id);

          if (user != null)
          {

            var oldFcm = user.fcm;
            string sessionStartedElsewhere = LocalizedTexts.GetTranslationByCountry(user.country, "sessionStartedElsewhere");


            if (!string.IsNullOrEmpty(oldFcm) && oldFcm != tokenRequest.token)
            {
              if (geo != null)
              {
                _ = _firebaseNotificationService.SendNotificationToUser(oldFcm, "Betrader", sessionStartedElsewhere, new() { { "type", "LOGOUT" }, { "city", geo.City! }, { "country", geo.Country! } });
              }
              else
              {
                _ = _firebaseNotificationService.SendNotificationToUser(oldFcm, "Betrader", sessionStartedElsewhere, new() { { "type", "LOGOUT" } });
              }
              _logger.Log.Information("[AUTH] :: RefreshFCM :: LogOut for FCM {fcm} of user {user}", oldFcm, user.username);
            }

            user.fcm = tokenRequest.token!;
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

    public static async Task<IpGeoResponse?> GetGeoLocationFromIp(string ip)
    {
      using var http = new HttpClient();
      var response = await http.GetAsync($"http://ip-api.com/json/{ip}");

      if (!response.IsSuccessStatusCode) return null;

      var json = await response.Content.ReadAsStringAsync();

      try
      {
        return JsonSerializer.Deserialize<IpGeoResponse>(json);
      }
      catch
      {
        return null;
      }
    }

    public class IpGeoResponse
    {
      [JsonPropertyName("country")]
      public string? Country { get; set; }

      [JsonPropertyName("regionName")]
      public string? RegionName { get; set; }

      [JsonPropertyName("city")]
      public string? City { get; set; }

      [JsonPropertyName("isp")]
      public string? ISP { get; set; }
    }
  }
}

