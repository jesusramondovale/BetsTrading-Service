using BetsTrading_Service.Controllers;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;
using BetsTrading_Service.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;

public class Program
{
  public static void Main(string[] args)
  {
    var logPath = "Logs/BetsTrading_Service_.log";
    RollingInterval loggingInterval = RollingInterval.Day;

    ICustomLogger customLogger = new CustomLogger(new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(logPath, rollingInterval: loggingInterval)
        .CreateLogger());

    try
    {
      customLogger.Log.Information("[PROGRAM] :: ******  ****** ****** ****** ****** ****** ****** STARTING BETSTRADING BACKEND SERVICE ****** ****** ****** ****** ****** ****** ******");

      var builder = WebApplication.CreateBuilder(args);
      builder.Services.AddSingleton(customLogger);

      customLogger.Log.Information("[PROGRAM] :: Serilog service started. Logging on {pth} with interval: {itr}", logPath, loggingInterval.ToString());

      var googleClientId = builder.Configuration["Google:ClientId"]!;
      var localIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://api.betstrading.online";
      var localAudience = builder.Configuration["Jwt:Audience"] ?? "bets-trading-api";
      var jwtLocalKey = Environment.GetEnvironmentVariable("JWT_LOCAL_KEY", EnvironmentVariableTarget.User) ?? "";

      if (jwtLocalKey.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local Custom Key is empty!");
      if (googleClientId.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: Google JWT Client Id is empty!");
      if (localIssuer.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local issuer is empty!");
      if (localAudience.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local audience is empty!");


      JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

      JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

      builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString),
        contextLifetime: ServiceLifetime.Scoped,
        optionsLifetime: ServiceLifetime.Scoped);

      builder.Services.AddMemoryCache();
      builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
      builder.Services.AddInMemoryRateLimiting();
      builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
      builder.Services.AddControllers();
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();
      builder.Services.AddTransient<AuthController>();
      builder.Services.AddTransient<InfoController>();
      builder.Services.AddTransient<FinancialAssetsController>();
      builder.Services.AddScoped<Updater>();
      builder.Services.AddScoped<FirebaseNotificationService>();
      builder.Services.AddHostedService<OddsAdjusterService>();
      builder.Services.AddHostedService<UpdaterHostedService>();
      builder.Services.AddSingleton<FirebaseNotificationService>();
      builder.Services.AddHsts(options =>
      {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(60);
        customLogger.Log.Information("[PROGRAM] :: Hsts max duration (days) : {d}", options.MaxAge.TotalSeconds / (3600 * 24));
      });
      builder.Services.AddHttpsRedirection(options =>
      {
        options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
        options.HttpsPort = 5001;
        customLogger.Log.Information("[PROGRAM] :: HTTPS redirection on port {prt}", options.HttpsPort);
      });
      builder.Services.AddResponseCompression(options =>
      {
        options.EnableForHttps = true;
      });
      builder.Services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = "Combined";
        options.DefaultChallengeScheme = "Combined";
      })
      .AddPolicyScheme("Combined", "Google+Local", options =>
      {
        options.ForwardDefaultSelector = context =>
        {
          var auth = context.Request.Headers["Authorization"].ToString();
          if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
          {
            var token = auth.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
              var jwt = handler.ReadJwtToken(token);
              var iss = jwt.Issuer?.Trim();
              if (iss == "https://accounts.google.com" || iss == "accounts.google.com")
                return "GoogleJwt";
            }
          }
          return "LocalJwt";
        };
      })
      .AddJwtBearer("GoogleJwt", o =>
      {
        o.Authority = "https://accounts.google.com";
        o.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
          ValidateAudience = true,
          ValidAudience = googleClientId,
          ValidateLifetime = true,
          NameClaimType = "sub",
          RoleClaimType = ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
          OnTokenValidated = async ctx =>
          {
            var googleSub = ctx.Principal!.FindFirstValue("sub");
            var email = ctx.Principal!.FindFirstValue("email")
                     ?? ctx.Principal!.FindFirstValue("emails")
                     ?? "";
            
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.id == googleSub || u.email == email);
            if (user == null)
            {
              ctx.Fail("User not registered");
              return;
            }

            var id = (ClaimsIdentity)ctx!.Principal!.Identity!;
            
            id.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.id));
            id.AddClaim(new Claim("app_sub", user.id));
            id.AddClaim(new Claim("auth_provider", "google"));
          }
        };
      })
      .AddJwtBearer("LocalJwt", options =>
      {
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = localIssuer,          // DEBE IGUALAR al que pones al emitir
          ValidateAudience = true,
          ValidAudience = localAudience,      // DEBE IGUALAR al que pones al emitir
          ValidateLifetime = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtLocalKey)), // misma clave
          NameClaimType = "sub",
          ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
          OnAuthenticationFailed = ctx => { Console.WriteLine($"[LocalJwt FAIL] {ctx.Exception.Message}"); return Task.CompletedTask; },
          OnChallenge = ctx => { Console.WriteLine($"[LocalJwt CHALLENGE] {ctx.Error} {ctx.ErrorDescription}"); return Task.CompletedTask; }
        };
      });

      builder.Services.AddAuthorization(options =>
      {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
          .RequireAuthenticatedUser()
          .Build();
      });

      builder.Services.AddControllers();

      var app = builder.Build();
      AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

      #if DEBUG
        customLogger.Log.Warning("[PROGRAM] :: Debug mode! Using SwaggerUI and Hsts");
        app.UseSwagger();
        app.UseSwaggerUI();
                
      #endif

     // Common middleware
      #if RELEASE
        app.UseIpRateLimiting();
      #endif
      app.UseResponseCompression();
      app.UseHttpsRedirection();
      app.UseAuthentication();
      app.UseAuthorization();
      app.MapControllers();
      customLogger.Log.Information("[PROGRAM] :: Controller endpoints added successfully");
      customLogger.Log.Information("[PROGRAM] :: All Backend services started successfully!");

      app.Run();

    }
    catch (Exception ex)
    {
      customLogger.Log.Error(ex, "[PROGRAM] :: Service terminated unexpectedly: {ErrorMessage}", ex.Message);
      throw;
    }
  }
}
