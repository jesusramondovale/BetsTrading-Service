using AspNetCoreRateLimit;
using BetsTrading_Service.Controllers;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace BetsTrading_Service
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var logPath = "Logs/BetsTrading_Service_.log";
      RollingInterval loggingInterval = RollingInterval.Day;

      ICustomLogger customLogger = new CustomLogger(new LoggerConfiguration()
          .MinimumLevel.Debug()  // Cambiar a Debug para capturar todos los logs
          .WriteTo.File(logPath, rollingInterval: loggingInterval)
          .WriteTo.Console()  // Añadir consola para ver logs en tiempo real
          .CreateLogger());

      try
      {
        customLogger.Log.Information("[PROGRAM] :: ****** ******  ****** STARTING BETSTRADING BACKEND SERVICE ****** ****** ******");

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton(customLogger);
        customLogger.Log.Information("[PROGRAM] :: Serilog service started. Logging on {pth} with interval: {itr}", logPath, loggingInterval.ToString());
        var googleClientId = builder.Configuration["Google:ClientId"]!;
        var localIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://api.betstrading.online";
        var localAudience = builder.Configuration["Jwt:Audience"] ?? "bets-trading-api";
        var jwtLocalKey = Environment.GetEnvironmentVariable("JWT_LOCAL_KEY") ?? "";

        if (jwtLocalKey.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local Custom Key is empty!");
        if (googleClientId.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: Google JWT Client Id is empty!");
        if (localIssuer.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local issuer is empty!");
        if (localAudience.IsNullOrEmpty()) Log.Logger.Fatal("[PROGRAM] :: JWT Local audience is empty!");


        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables();
        builder.Services.Configure<SmtpSettings>(settings =>
        {
          builder.Configuration.GetSection("SMTP").Bind(settings);
          var envPassword = Environment.GetEnvironmentVariable("SMTP__Password");
          if (!string.IsNullOrEmpty(envPassword))
          {
            settings.Password = envPassword;
          }
        });

        builder.Services.AddSingleton<IEmailService>(sp =>
        {
          var settings = sp.GetRequiredService<IOptions<SmtpSettings>>().Value;
          return new EmailService(settings);
        });
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString),
          contextLifetime: ServiceLifetime.Scoped,
          optionsLifetime: ServiceLifetime.Scoped);

        builder.Services.AddMemoryCache();
        builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
        builder.Services.AddInMemoryRateLimiting();
        builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Configure JSON options to be case-insensitive and handle both camelCase and PascalCase
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use exact property names as defined
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddTransient<AuthController>();
        builder.Services.AddTransient<InfoController>();
        builder.Services.AddTransient<FinancialAssetsController>();
        builder.Services.AddScoped<UpdaterService>();
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
            var auth = context.Request.Headers.Authorization.ToString();
            customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Auth Header: {auth}", string.IsNullOrEmpty(auth) ? "EMPTY" : auth.Substring(0, Math.Min(50, auth.Length)) + "...");
            
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
              var token = auth.Substring("Bearer ".Length).Trim();
              var handler = new JwtSecurityTokenHandler();
              if (handler.CanReadToken(token))
              {
                try
                {
                  var jwt = handler.ReadJwtToken(token);
                  var iss = jwt.Issuer?.Trim();
                  customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Token Issuer: {iss}", iss ?? "null");
                  if (iss == "https://accounts.google.com" || iss == "accounts.google.com")
                  {
                    customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Using GoogleJwt");
                    return "GoogleJwt";
                  }
                  else
                  {
                    customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Using LocalJwt (issuer: {iss})", iss ?? "null");
                    return "LocalJwt";
                  }
                }
                catch (Exception ex)
                {
                  customLogger.Log.Warning("[AUTH] :: Combined Scheme Selector :: Error reading token: {msg}", ex.Message);
                  // Si hay un token pero no se puede leer, intentar con LocalJwt
                  return "LocalJwt";
                }
              }
              else
              {
                customLogger.Log.Warning("[AUTH] :: Combined Scheme Selector :: Cannot read token");
                // Si hay un token pero no se puede leer, intentar con LocalJwt
                return "LocalJwt";
              }
            }
            else
            {
              // Si no hay token, usar LocalJwt pero permitir que falle silenciosamente
              // El OnChallenge manejará esto y permitirá que el request continúe para endpoints con [AllowAnonymous]
              customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: No Bearer token, using LocalJwt (will allow request to continue if [AllowAnonymous])");
              return "LocalJwt";
            }
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
          // No requerir token si no está presente - permitir que el request continúe sin autenticación
          options.RequireHttps = false;
          // Permitir que el request continúe sin token - el controlador decidirá si requiere autenticación
          options.SaveToken = true;
          options.Events = new JwtBearerEvents
          {
            OnAuthenticationFailed = ctx => 
            { 
              var authHeader = ctx.Request.Headers["Authorization"].ToString();
              var path = ctx.Request.Path;
              customLogger.Log.Information("[AUTH] :: LocalJwt FAIL :: Path: {path}, Exception: {msg}, Auth Header Present: {hasHeader}", 
                path, ctx.Exception?.Message ?? "null", !string.IsNullOrEmpty(authHeader));
              if (ctx.Exception != null)
              {
                customLogger.Log.Information("[AUTH] :: LocalJwt FAIL :: Exception Type: {type}, StackTrace: {stack}", 
                  ctx.Exception.GetType().Name, ctx.Exception.StackTrace ?? "null");
              }
              var tokenPreview = string.IsNullOrEmpty(authHeader) ? "EMPTY" : 
                (authHeader.Length > 100 ? authHeader.Substring(0, 100) + "..." : authHeader);
              Console.WriteLine($"[LocalJwt FAIL] Path: {path}, Exception: {ctx.Exception?.Message ?? "null"}, AuthHeader: {tokenPreview}");
              // No fallar la autenticación, permitir que continúe sin autenticación
              // Esto es especialmente importante para endpoints con [AllowAnonymous]
              ctx.NoResult();
              return Task.CompletedTask; 
            },
            OnChallenge = ctx => 
            { 
              var authHeader = ctx.Request.Headers["Authorization"].ToString();
              var path = ctx.Request.Path;
              var hasToken = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
              
              // Verificar si el usuario ya está autenticado (token válido)
              var isAuthenticated = ctx.HttpContext.User?.Identity?.IsAuthenticated ?? false;
              
              // Si Error y ErrorDescription son null, significa que OnChallenge se ejecutó sin un error real
              // Esto puede suceder cuando el middleware de autorización requiere autenticación pero no hay error de token
              var hasRealError = !string.IsNullOrEmpty(ctx.Error) || !string.IsNullOrEmpty(ctx.ErrorDescription);
              
              // Log detallado con Information para asegurar que se capture
              customLogger.Log.Information("[AUTH] :: LocalJwt CHALLENGE :: Path: {path}, Error: {error}, Description: {description}, Auth Header Present: {hasHeader}, HasToken: {hasToken}, IsAuthenticated: {isAuth}, HasRealError: {hasError}", 
                path, ctx.Error ?? "null", ctx.ErrorDescription ?? "null", 
                !string.IsNullOrEmpty(authHeader), hasToken, isAuthenticated, hasRealError);
              
              // Console.WriteLine más detallado
              var tokenPreview = string.IsNullOrEmpty(authHeader) ? "EMPTY" : 
                (authHeader.Length > 100 ? authHeader.Substring(0, 100) + "..." : authHeader);
              Console.WriteLine($"[LocalJwt CHALLENGE] Path: {path}, Error: {ctx.Error ?? "null"}, Description: {ctx.ErrorDescription ?? "null"}, HasToken: {hasToken}, IsAuthenticated: {isAuthenticated}, HasRealError: {hasRealError}, AuthHeader: {tokenPreview}");
              
              // Si el usuario ya está autenticado, permitir que continúe
              if (isAuthenticated)
              {
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: User already authenticated for {path}, allowing request to continue", path);
                ctx.HandleResponse();
                return Task.CompletedTask;
              }
              
              // Si hay un token pero no hay error real y no está autenticado, podría ser un problema de timing
              // En este caso, permitir que la petición continúe y dejar que el controlador maneje la autorización
              // Esto es especialmente importante porque OnAuthenticationFailed ya manejó el error con ctx.NoResult()
              if (hasToken && !hasRealError)
              {
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: Token present but no real error for {path}, allowing request to continue (controller will handle authorization)", path);
                ctx.HandleResponse();
                return Task.CompletedTask;
              }
              
              // SOLO para endpoints que tenían [AllowAnonymous] ANTES del commit 7efa12453b5be3e8a835dd5923b5112bd4f1cfd8
              var isPublicEndpoint = path.Value != null && (
                path.Value.Contains("/AddAps", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/SendCode", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/SignIn", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/GoogleQuickRegister", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/LogIn", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/ResetPassword", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/Webhook", StringComparison.OrdinalIgnoreCase) ||
                path.Value.Contains("/VerifyAd", StringComparison.OrdinalIgnoreCase)
              );
              
              // Si es un endpoint público, permitir que continúe sin autenticación
              if (isPublicEndpoint)
              {
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: Allowing request to continue for {path} (public endpoint with [AllowAnonymous])", path);
                ctx.HandleResponse();
                return Task.CompletedTask;
              }
              
              // Si hay un token pero hay un error real, el token es inválido/expirado
              if (hasToken && hasRealError)
              {
                customLogger.Log.Information("[AUTH] :: LocalJwt CHALLENGE :: Token present but invalid for {path} (error: {error}) - RETURNING 401", path, ctx.Error);
                Console.WriteLine($"[LocalJwt CHALLENGE] Token present but invalid for {path} - RETURNING 401");
                // NO llamar a HandleResponse() - dejar que el sistema devuelva 401 normalmente
                return Task.CompletedTask;
              }
              
              // No hay token y no es un endpoint público - devolver 401
              customLogger.Log.Information("[AUTH] :: LocalJwt CHALLENGE :: No token for {path} (requires authentication) - RETURNING 401", path);
              Console.WriteLine($"[LocalJwt CHALLENGE] No token for {path} - RETURNING 401");
              // NO llamar a HandleResponse() - dejar que el sistema devuelva 401 normalmente
              return Task.CompletedTask; 
            },
            OnTokenValidated = ctx =>
            {
              var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? ctx.Principal?.FindFirstValue("sub");
              customLogger.Log.Debug("[AUTH] :: LocalJwt :: Token validated successfully. UserId: {userId}", userId ?? "null");
              return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
              var token = ctx.Token;
              var path = ctx.Request.Path;
              customLogger.Log.Debug("[AUTH] :: LocalJwt :: Message received. Token present: {hasToken}, Token length: {length}, Path: {path}", 
                !string.IsNullOrEmpty(token), token?.Length ?? 0, path);
              
              // No hacer nada especial aquí - el OnChallenge manejará los endpoints públicos
              return Task.CompletedTask;
            }
          };
        });

        // No requerir autenticación por defecto - cada endpoint decidirá si requiere autenticación
        // Esto permite que [AllowAnonymous] funcione correctamente
        builder.Services.AddAuthorizationBuilder();

        // Controllers already configured above with JSON options

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

        // Mmigrate EF (only Linux)
        using (var scope = app.Services.CreateScope())
        {
          var services = scope.ServiceProvider;
          try
          {
            var context = services.GetRequiredService<AppDbContext>();

            customLogger.Log.Information("[PROGRAM] :: Aplicando migraciones de Entity Framework Core...");
            context.Database.Migrate();
            customLogger.Log.Information("[PROGRAM] :: Migraciones aplicadas correctamente.");

          }
          catch (Exception ex)
          {
            customLogger.Log.Error(ex, "[PROGRAM] :: Ocurrió un error al aplicar las migraciones de la base de datos. La aplicación no continuará.");
          }
        }

        app.Run();

      }
      catch (Exception ex)
      {
        customLogger.Log.Error(ex, "[PROGRAM] :: Service terminated unexpectedly: {ErrorMessage}", ex.Message);
        throw;
      }
    }
  }
}