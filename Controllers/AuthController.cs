using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using BetsTrading_Service.Services;
using BetsTrading_Service.Locale;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.IO;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController(AppDbContext dbContext, IConfiguration config, ICustomLogger customLogger, FirebaseNotificationService firebaseNotificationService, IEmailService emailService) : ControllerBase
  {
    private const int SESSION_EXP_DAYS= 15;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ICustomLogger _logger = customLogger;
    private readonly FirebaseNotificationService _firebaseNotificationService = firebaseNotificationService;
    private readonly IConfiguration _config = config;
    private readonly IEmailService _emailService = emailService;

    private string GenerateLocalJwt(string userId, string email, string? name)
    {
      var issuer = _config["Jwt:Issuer"]!;
      var audience = _config["Jwt:Audience"]!;
      var key = Environment.GetEnvironmentVariable("JWT_LOCAL_KEY")
               ?? _config["Jwt:Key"]!;

      var claims = new[]
      {
        new Claim(JwtRegisteredClaimNames.Sub, userId),             // ID interno
        new Claim(ClaimTypes.NameIdentifier, userId),               // por si acaso
        new Claim(JwtRegisteredClaimNames.Email, email ?? ""),
        new Claim("name", name ?? ""),
        new Claim("auth_provider", "local"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
      };

      var creds = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        SecurityAlgorithms.HmacSha256);

      var now = DateTime.UtcNow;
      var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: now,
        expires: now.AddHours(12),
        signingCredentials: creds);

      return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [AllowAnonymous]
    [HttpPost("SendCode")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest req)
    {
      try
      {
        string email = req.Email ?? "";

        if (string.IsNullOrWhiteSpace(email))
        {
          return BadRequest(new { success = false, message = "Email is required." });
        }

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.email == email);
        if (existingUser != null)
        {
          return Conflict(new { success = false, message = "Email already exists." });
        }

        var random = new Random();
        string code = random.Next(100000, 999999).ToString();
        
        var verificationCode = new VerificationCode(
          email,
          code,
          DateTime.UtcNow,
          DateTime.UtcNow.AddMinutes(10)
        );
        
        var oldCodes = _dbContext.VerificationCodes.Where(c => c.email == email && !c.verified);
        _dbContext.VerificationCodes.RemoveRange(oldCodes);
        
        await _dbContext.VerificationCodes.AddAsync(verificationCode);
        await _dbContext.SaveChangesAsync();

        string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(req.Country ?? "UK", "emailCodeSentBody");
        string localedBody = string.Format(localedBodyTemplate, code);

        await _emailService.SendEmailAsync(
            to: req.Email ?? "",
            subject: LocalizedTexts.GetTranslationByCountry(req.Country ?? "UK", "emailSubjectCode"),
            body: localedBody
        );

        _logger.Log.Debug($"[AUTH] :: Verification code sent for {email} - Code : {code}");

        return Ok(new { success = true, message = "Verification code sent successfully." });
      }
      catch (Exception ex)
      {
        _logger.Log.Error($"[AUTH] :: Error sending verification code: {ex.Message}");
        return StatusCode(500, new { success = false, message = "Internal server error." });
      }
    }

    private async Task<IActionResult> RegisterInternal(SignUpRequest signUpRequest, bool googleQuickMode)
    {
      var transaction = await _dbContext.Database.BeginTransactionAsync();
      {
        try
        {
          
          if (!googleQuickMode && (string.IsNullOrWhiteSpace(signUpRequest.Email) || string.IsNullOrWhiteSpace(signUpRequest.EmailCode)))
          {
            return BadRequest(new { success = false, message = "Email and verification code are required." });
          }

          VerificationCode? verification = null;

          if (!googleQuickMode)
          {
            verification = await _dbContext.VerificationCodes
              .FirstOrDefaultAsync(v =>
                  v.email == signUpRequest.Email &&
                  v.code == signUpRequest.EmailCode &&
                  v.verified == false &&
                  v.expiresAt > DateTime.UtcNow);

            if (verification == null)
            {
              return BadRequest(new { success = false, message = "Invalid or expired verification code." });
            }
          }
          
          var existingUser = await _dbContext.Users
              .FirstOrDefaultAsync(u => u.username == signUpRequest.Username || u.email == signUpRequest.Email);

          if (existingUser != null)
          {
            return Conflict(new { success = false, message = "Username, email or ID already exists." });
          }

          
          string hashedPassword = (signUpRequest.Password != null
              ? BCrypt.Net.BCrypt.HashPassword(signUpRequest.Password)
              : "nullPassword");

          var guid = signUpRequest.Token ?? Guid.NewGuid().ToString();

          var newUser = new User(
              guid,
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
              0)
          {
            is_active = true,
            token_expiration = DateTime.UtcNow.AddDays(SESSION_EXP_DAYS)
          };
          var jwt = GenerateLocalJwt(guid, signUpRequest.Email!, signUpRequest.FullName);
          _dbContext.Users.Add(newUser);

          if (!googleQuickMode)
          {
            verification!.verified = true;
            _dbContext.VerificationCodes.Update(verification);
          }
          await _dbContext.SaveChangesAsync();

          var newBTCFavorite = new Favorite(id: Guid.NewGuid().ToString(), user_id: guid, ticker: "BTC");
          var newGOOGFavorite = new Favorite(id: Guid.NewGuid().ToString(), user_id: guid, ticker: "GOOG.NASDAQ");
          var newNVDAFavorite = new Favorite(id: Guid.NewGuid().ToString(), user_id: guid, ticker: "NVDA.NASDAQ");
          _dbContext.Favorites.AddRange(newBTCFavorite, newGOOGFavorite, newNVDAFavorite);

          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();

          string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(signUpRequest.Country!, "registrationSuccessfullEmailBody");
          string localedBody = string.Format(localedBodyTemplate, signUpRequest.FullName);

          if (_emailService != null && !_emailService.ToString().IsNullOrEmpty()) {
            await _emailService.SendEmailAsync(
              to: signUpRequest.Email ?? "",
              subject: LocalizedTexts.GetTranslationByCountry(signUpRequest.Country ?? "UK", "emailSubjectWelcome"),
              body: localedBody
            );
          }
          

          _logger.Log.Information("[AUTH] :: Register :: Success with user ID : {userID}", newUser.id);

          return Ok(new
          {
            success = true,
            message = "Registration successful!",
            userId = newUser.id,
            jwtToken = jwt
          });
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[AUTH] :: Register :: Error : {msg}", ex.Message);
          return StatusCode(500, new { success = false, message = "Internal server error!", error = ex.Message });
        }
      }
    }

    [AllowAnonymous]
    [HttpPost("SignIn")]
    public Task<IActionResult> SignIn([FromBody] SignUpRequest req)
    {
      return RegisterInternal(req, false);
    }

    [AllowAnonymous]
    [HttpPost("GoogleQuickRegister")]
    public async Task<IActionResult> GoogleQuickRegister(googleSignRequest isGoogledRequest)
    {

      Console.WriteLine("GoogleQuickRegister() called");
      SignUpRequest signUpRequest = new()
      {
        Token = isGoogledRequest.id
      };
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
      var signInResult = await RegisterInternal(signUpRequest, true);


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

    [AllowAnonymous]
    [HttpPost("LogIn")]
    public async Task<IActionResult> LogIn([FromBody] Requests.LoginRequest loginRequest)
    {
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
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

            var jwt = GenerateLocalJwt(user.id, user.email, user.fullname);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();


            _logger.Log.Information("[AUTH] :: LogIn :: Success. User ID: {userId} FROM {city} , {region} , {country} , ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country, geo.ISP);
            return Ok(new { Message = "LogIn SUCCESS", UserId = user.id, jwtToken = jwt });
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

    [AllowAnonymous]
    [HttpPost("ResetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] idRequest request)
    {
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        if (string.IsNullOrEmpty(request.id))
          return BadRequest(new { Message = "Email/ID required" });

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.email == request.id);
        if (user == null)
          return NotFound(new { Message = "User not found" });

        string newPassword = GenerateSecurePassword();

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.password = hashedPassword;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();

        string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "resetPasswordEmailBody");

        string localedBody = string.Format(localedBodyTemplate, user.fullname, newPassword);

        await _emailService.SendEmailAsync(
            to: user.email,
            subject: LocalizedTexts.GetTranslationByCountry(user.country ?? "UK", "emailSubjectPassword"),
            body: localedBody
        );

        await transaction.CommitAsync();

        _logger.Log.Information("[AUTH] :: NewPassword :: Success. User ID: {userId}", user.id);

        return Ok(new { Message = "New password generated and sent by email" });
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[AUTH] :: NewPassword :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewPassword")]
    public async Task<IActionResult> NewPassword([FromBody] Requests.LoginRequest newPasswordRequest)
    {

      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(newPasswordRequest.Username) &&
            !string.Equals(newPasswordRequest.Username, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

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
        _logger.Log.Error("[AUTH] :: NewPassword :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] Requests.ChangePasswordRequest changepasswordRequest)
    {
      if (changepasswordRequest.Current == changepasswordRequest.Password) return BadRequest(new { Message = "New password matches current password" });

      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(changepasswordRequest.Username) &&
            !string.Equals(changepasswordRequest.Username, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

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


          string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "newPasswordEmailBody");

          string localedBody = string.Format(localedBodyTemplate, user.fullname);

          await _emailService.SendEmailAsync(
              to: user.email,
              subject: LocalizedTexts.GetTranslationByCountry(user.country ?? "UK", "emailSubjectPassword"),
              body: localedBody
          );

          _logger.Log.Information("[AUTH] :: ChangePassword :: Success. User ID: {userId} FROM -> {city} , {region} , {country} , ISP: {isp}", user.id, geo!.City, geo.RegionName, geo.Country, geo.ISP);
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

    [HttpPost("GoogleLogIn")]
    public async Task<IActionResult> GoogleLogIn([FromBody] Requests.LoginRequest? loginRequest)
    {    
      try
      {
        // Handle model binding errors (e.g., malformed JSON, empty body)
        if (loginRequest == null)
        {
          // Try to read raw body to provide better error message
          try
          {
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
            
            if (string.IsNullOrWhiteSpace(rawBody))
            {
              _logger.Log.Warning("[AUTH] :: Google LogIn :: Empty request body");
              return BadRequest(new { Message = "Request body is required. Expected JSON: {\"Username\": \"your-google-user-id\"}" });
            }
            
            // If body exists but model binding failed, it's likely malformed JSON
            _logger.Log.Warning("[AUTH] :: Google LogIn :: Invalid JSON format. Body: {body}", rawBody.Substring(0, Math.Min(100, rawBody.Length)));
            return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"Username\": \"your-google-user-id\"}" });
          }
          catch
          {
            _logger.Log.Warning("[AUTH] :: Google LogIn :: Request body is required");
            return BadRequest(new { Message = "Request body is required. Expected JSON: {\"Username\": \"your-google-user-id\"}" });
          }
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(loginRequest.Username))
        {
          _logger.Log.Warning("[AUTH] :: Google LogIn :: Username is required");
          return BadRequest(new { Message = "Username is required" });
        }

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
          _logger.Log.Debug("[AUTH] :: Google LogIn :: Sucess. User ID: {userId} from {city} , {region} , {country}. ISP: {isp}", user.id, geo?.City ?? "Unknown", geo?.RegionName ?? "Unknown", geo?.Country ?? "Unknown", geo?.ISP ?? "Unknown");
          return Ok(new { Message = "Google LogIn SUCCESS", UserId = user.id });

        }
        else // Unexistent user
        {
          _logger.Log.Warning("[AUTH] :: Google LogIn :: Unexistent user : {userRequest} ", loginRequest.Username);
          return NotFound(new { Message = "User not found" }); // User not found
        }
      }
      catch (System.Text.Json.JsonException jsonEx)
      {
        // Handle JSON parsing errors specifically
        _logger.Log.Error("[AUTH] :: Google LogIn :: JSON parsing error: {msg}", jsonEx.Message);
        return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
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
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(logOutRequest.id) &&
            !string.Equals(logOutRequest.id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

        var user = _dbContext.Users.FirstOrDefault(u => u.id == logOutRequest.id);

        if (user != null)
        {
          user.last_session = DateTime.UtcNow;
          user.is_active = false;
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();

          _logger.Log.Debug("[AUTH] :: LogOut :: Success on user {username}", user.username);
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
            _logger.Log.Debug("[AUTH] :: IsLoggedIn :: Session active on id {id}", isLoggedRequest.id);
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
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(tokenRequest.user_id) &&
            !string.Equals(tokenRequest.user_id, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

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

          _logger.Log.Debug("[AUTH] :: RefreshFCM :: Success with User ID {id}", tokenRequest.user_id);
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

    private static string GenerateSecurePassword(int length = 12)
    {
      const string lower = "abcdefghijklmnopqrstuvwxyz";
      const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
      const string digits = "0123456789";
      const string all = lower + upper + digits;

      var rnd = new Random();
      var passwordChars = new List<char>
    {
        upper[rnd.Next(upper.Length)],   // Garantizar al menos una mayúscula
        digits[rnd.Next(digits.Length)]  // Garantizar al menos un dígito
    };

      for (int i = passwordChars.Count; i < length; i++)
      {
        passwordChars.Add(all[rnd.Next(all.Length)]);
      }

      // Mezclar caracteres
      return new string(passwordChars.OrderBy(_ => rnd.Next()).ToArray());
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

