using Microsoft.EntityFrameworkCore;
using BetsTrading.Infrastructure.Persistence;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Mappings;
using System.Reflection;
using FluentValidation;
using MediatR;
using BetsTrading.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Serilog;
using BetsTrading.Infrastructure.Logging;
using BetsTrading.Application.Services;
using System.Net;
using AspNetCoreRateLimit;
using Stripe;
using Npgsql;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting;
using BetsTrading.API.Health;
using BetsTrading.API.Security;
using Microsoft.Extensions.Caching.Memory;

try
{
    var builder = WebApplication.CreateBuilder(args);

// Systemd integration (Type=notify) - no-op when not running under systemd
builder.Host.UseSystemd();

// Configure Serilog - logs next to the executable (bin\Release\net8.0\Logs or publish folder)
var loggingInterval = Serilog.RollingInterval.Day;
var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
var logDirectory = Path.Combine(baseDir, "Logs");
var logFileName = "BetsTrading_API_.log";
var logPath = Path.Combine(logDirectory, logFileName);

if (!Directory.Exists(logDirectory))
{
    try
    {
        Directory.CreateDirectory(logDirectory);
    }
    catch (Exception)
    {
        logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);
        logPath = Path.Combine(logDirectory, logFileName);
    }
}

var fullLogPath = Path.GetFullPath(logPath);

var logger = new LoggerConfiguration()
    .MinimumLevel.Is(builder.Environment.IsDevelopment() ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Fatal)
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logPath, rollingInterval: loggingInterval)
    .CreateLogger();

var customLogger = new CustomLogger((Serilog.Core.Logger)logger);
builder.Services.AddSingleton<ICustomLogger>(customLogger);
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IApplicationLogger>(sp =>
    new BetsTrading.Infrastructure.Logging.ApplicationLogger(sp.GetRequiredService<ICustomLogger>()));

customLogger.Log.Information("[PROGRAM] :: ****** STARTING BETSTRADING API (Clean Architecture) ******");
customLogger.Log.Information("[PROGRAM] :: Working Directory: {wd}", Directory.GetCurrentDirectory());
customLogger.Log.Information("[PROGRAM] :: Serilog service started. Logging on {pth} (full path: {full}) with interval: {itr}", 
    logPath, fullLogPath, loggingInterval.ToString());
customLogger.Log.Information("[PROGRAM] :: Logs are also displayed in the console in real-time.");

// ForwardedHeaders: necesario cuando Cloudflare/Nginx actúan como proxy
// Permite que la app reconozca el protocolo real (HTTPS) aunque reciba HTTP desde el proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                ForwardedHeaders.XForwardedProto |
                                ForwardedHeaders.XForwardedHost;
    // Confiar en localhost (Nginx) y en Cloudflare (rango de IPs conocido)
    // En producción, Nginx está en localhost y Cloudflare envía headers confiables
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    // Permitir todos los proxies (Cloudflare y Nginx localhost)
    // En producción esto es seguro porque solo Nginx puede acceder a localhost:5000
});

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON options to use camelCase for compatibility with client
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; // Use camelCase to match client expectations
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 64; // Increase max depth if needed
        options.JsonSerializerOptions.WriteIndented = false; // Compact JSON for performance
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database - Get connection string first
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_ADMIN_PASSWORD");

// Si hay variable de entorno para la contraseña, construir la cadena de conexión dinámicamente
if (!string.IsNullOrEmpty(postgresPassword) && !string.IsNullOrEmpty(connectionString))
{
    // Reemplazar la contraseña en la cadena de conexión
    var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    connectionStringBuilder.Password = postgresPassword;
    connectionString = connectionStringBuilder.ToString();
    customLogger.Log.Information("[PROGRAM] :: Using PostgreSQL password from environment variable POSTGRES_ADMIN_PASSWORD");
}
else if (string.IsNullOrEmpty(postgresPassword))
{
    customLogger.Log.Warning("[PROGRAM] :: POSTGRES_ADMIN_PASSWORD environment variable not configured, using password from appsettings.json");
}

// Health Checks
// Health Checks - Simple check that doesn't require database connection
// This ensures the endpoint responds even if database is temporarily unavailable
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"));
    // Database check removed to avoid blocking health endpoint if DB is temporarily unavailable
    // .AddNpgSql(connectionString ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."),
    //     name: "database",
    //     tags: new[] { "db", "sql", "postgresql" });

// CORS
// CORS configuration - Allow all origins since requests come through Cloudflare proxy
// Legacy project doesn't have CORS configured, so we match that behavior
// CORS configuration removed - matching legacy Program.cs (no CORS configuration)

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    // Disable detailed SQL query logging to reduce noise in logs
    // Only warnings and errors from EF Core will be logged
    options.LogTo(_ => { }, LogLevel.Warning);
},
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Scoped);

// UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(BetsTrading.Application.Commands.Bets.CreateBetCommand).Assembly));
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(BetsTrading.Application.Behaviors.ValidationBehavior<,>));

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(BetsTrading.Application.Commands.Bets.CreateBetCommandValidator).Assembly);

// JWT Configuration
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") 
    ?? builder.Configuration["Google:ClientId"] 
    ?? "";
var localIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://api.betstrading.online";
var localAudience = builder.Configuration["Jwt:Audience"] ?? "bets-trading-api";
var jwtLocalKey = Environment.GetEnvironmentVariable("JWT_LOCAL_KEY") ?? builder.Configuration["Jwt:Key"] ?? "";

if (string.IsNullOrEmpty(jwtLocalKey))
    customLogger.Log.Warning("[PROGRAM] :: JWT Local Custom Key is empty!");
if (string.IsNullOrEmpty(googleClientId))
    customLogger.Log.Warning("[PROGRAM] :: Google JWT Client Id is empty! (Set GOOGLE_CLIENT_ID environment variable or configure in appsettings.json)");
else
    customLogger.Log.Information("[PROGRAM] :: Google JWT Client Id loaded successfully.");

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Authentication
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
        customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Auth Header: {auth}", 
            string.IsNullOrEmpty(auth) ? "EMPTY" : auth.Substring(0, Math.Min(50, auth.Length)) + "...");
        
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
                    customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Error reading token: {msg}", ex.Message);
                    return "LocalJwt";
                }
              }
              else
              {
                customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: Cannot read token");
                return "LocalJwt";
              }
        }
        else
        {
            customLogger.Log.Debug("[AUTH] :: Combined Scheme Selector :: No Bearer token, using LocalJwt");
            return "LocalJwt";
        }
    };
})
.AddJwtBearer("GoogleJwt", o =>
{
    if (string.IsNullOrEmpty(googleClientId))
    {
        customLogger.Log.Warning("[PROGRAM] :: Google JWT Client Id is empty! Google authentication will not work.");
        // Configurar con valores por defecto para evitar errores de configuración
        o.Authority = "https://accounts.google.com";
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };
    }
    else
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
                try
                {
                    var path = ctx.Request.Path;
                    var googleSub = ctx.Principal!.FindFirstValue("sub");
                    var email = ctx.Principal!.FindFirstValue("email")
                             ?? ctx.Principal!.FindFirstValue("emails")
                             ?? "";

                    customLogger.Log.Debug("[AUTH] :: GoogleJwt OnTokenValidated :: Path: {path}, GoogleSub: {sub}, Email: {email}", 
                        path, googleSub ?? "null", email ?? "null");

                    var unitOfWork = ctx.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                    
                    // Buscar primero por ID (googleSub), luego por email
                    // En el proyecto legacy, el usuario se guarda con id = googleSub
                    BetsTrading.Domain.Entities.User? user = null;
                    if (!string.IsNullOrEmpty(googleSub))
                    {
                        user = await unitOfWork.Users.GetByIdAsync(googleSub);
                        if (user != null)
                        {
                            customLogger.Log.Debug("[AUTH] :: GoogleJwt OnTokenValidated :: User found by ID: {id}", googleSub);
                        }
                    }
                    
                    // Si no se encontró por ID, buscar por email
                    if (user == null && !string.IsNullOrEmpty(email))
                    {
                        user = await unitOfWork.Users.GetByEmailAsync(email);
                        if (user != null)
                        {
                            customLogger.Log.Debug("[AUTH] :: GoogleJwt OnTokenValidated :: User found by Email: {email}", email);
                        }
                    }
                    
                    if (user == null)
                    {
                        customLogger.Log.Debug("[AUTH] :: GoogleJwt OnTokenValidated :: User not found for GoogleSub: {sub}, Email: {email}", googleSub, email);
                        ctx.Fail("User not registered");
                        return;
                    }

                    var id = (ClaimsIdentity)ctx!.Principal!.Identity!;
                    id.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
                    id.AddClaim(new Claim("app_sub", user.Id));
                    id.AddClaim(new Claim("auth_provider", "google"));
                    
                    customLogger.Log.Debug("[AUTH] :: GoogleJwt OnTokenValidated :: Claims added. UserId: {userId}, IsAuthenticated: {isAuth}", 
                        user.Id, ctx.Principal.Identity?.IsAuthenticated ?? false);
                }
                catch (Exception ex)
                {
                    customLogger.Log.Error(ex, "[PROGRAM] :: Error in GoogleJwt OnTokenValidated: {Error}", ex.Message);
                    ctx.Fail("Internal error during token validation");
                }
            }
        };
    }
})
.AddJwtBearer("LocalJwt", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = localIssuer,
        ValidateAudience = true,
        ValidAudience = localAudience,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtLocalKey)),
        NameClaimType = "sub",
        ClockSkew = TimeSpan.FromMinutes(2)
    };
    options.SaveToken = true;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Token;
            var path = ctx.Request.Path;
            customLogger.Log.Debug("[AUTH] :: LocalJwt :: Message received. Path: {path}, Token present: {hasToken}, Token length: {length}", 
                path, !string.IsNullOrEmpty(token), token?.Length ?? 0);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? ctx.Principal?.FindFirstValue("sub");
            var path = ctx.Request.Path;
            customLogger.Log.Debug("[AUTH] :: LocalJwt :: Token validated successfully. Path: {path}, UserId: {userId}", path, userId ?? "null");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            var authHeader = ctx.Request.Headers["Authorization"].ToString();
            var path = ctx.Request.Path;
            customLogger.Log.Debug("[AUTH] :: LocalJwt FAIL :: Path: {path}, Exception: {msg}, Auth Header Present: {hasHeader}", 
                path, ctx.Exception?.Message ?? "null", !string.IsNullOrEmpty(authHeader));
            if (ctx.Exception != null)
            {
                customLogger.Log.Debug("[AUTH] :: LocalJwt FAIL :: Exception Type: {type}", 
                    ctx.Exception.GetType().Name);
            }
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
            var isAuthenticated = ctx.HttpContext.User?.Identity?.IsAuthenticated ?? false;
            var hasRealError = !string.IsNullOrEmpty(ctx.Error) || !string.IsNullOrEmpty(ctx.ErrorDescription);
            
            customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: Path: {path}, Error: {error}, Description: {description}, HasToken: {hasToken}, IsAuthenticated: {isAuth}, HasRealError: {hasError}", 
                path, ctx.Error ?? "null", ctx.ErrorDescription ?? "null", hasToken, isAuthenticated, hasRealError);
            
            // Skip challenge for health/status check endpoints
            if (ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/status") || ctx.Request.Path.StartsWithSegments("/info"))
            {
                ctx.HandleResponse();
                return Task.CompletedTask;
            }
            
            // Si el usuario ya está autenticado, permitir que continúe
            if (isAuthenticated)
            {
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: User already authenticated for {path}, allowing request to continue", path);
                ctx.HandleResponse();
                return Task.CompletedTask;
            }
            
            // Si hay un token pero no hay error real, permitir que continúe (el controlador manejará la autorización)
            if (hasToken && !hasRealError)
            {
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: Token present but no real error for {path}, allowing request to continue", path);
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
                customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: Token present but invalid for {path} (error: {error}) - RETURNING 401", path, ctx.Error);
                return Task.CompletedTask;
            }
            
            // No hay token y no es un endpoint público - devolver 401
            customLogger.Log.Debug("[AUTH] :: LocalJwt CHALLENGE :: No token for {path} (requires authentication) - RETURNING 401", path);
            return Task.CompletedTask;
        }
    };
});

// Authorization
        // No requerir autenticación por defecto - cada endpoint decidirá si requiere autenticación
        // Esto permite que [AllowAnonymous] funcione correctamente
        builder.Services.AddAuthorizationBuilder();

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
});

// HTTPS Redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
    options.HttpsPort = 5001;
});

// Npgsql Legacy Timestamp Behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Email Service (SMTP)
// Host, Username, FromAddress, Password pueden venir de appsettings o de .env (SMTP__Host, SMTP__Username, SMTP__FromAddress, SMTP__Password).
builder.Services.Configure<BetsTrading.Infrastructure.Services.SmtpSettings>(settings =>
{
    builder.Configuration.GetSection("SMTP").Bind(settings);
    var envHost = Environment.GetEnvironmentVariable("SMTP__Host")?.Trim();
    var envUser = Environment.GetEnvironmentVariable("SMTP__Username")?.Trim();
    var envFrom = Environment.GetEnvironmentVariable("SMTP__FromAddress")?.Trim();
    var envPass = Environment.GetEnvironmentVariable("SMTP__Password")?.Trim();
    if (!string.IsNullOrEmpty(envHost)) settings.Host = envHost;
    if (!string.IsNullOrEmpty(envUser)) settings.Username = envUser;
    if (!string.IsNullOrEmpty(envFrom)) settings.FromAddress = envFrom;
    if (!string.IsNullOrEmpty(envPass)) settings.Password = envPass;
});

// HttpClient factory and named clients (reuse connections, avoid socket exhaustion)
builder.Services.AddHttpClient("TwelveData", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("IpGeo");
builder.Services.AddHttpClient("Didit");
builder.Services.AddHttpClient("AdMob");

// AdMob SSV Verifier (usa IHttpClientFactory)
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IAdMobSsvVerifier, BetsTrading.Infrastructure.Services.AdMobSsvVerifierService>();

// Didit API Service
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IDiditApiService, BetsTrading.Infrastructure.Services.DiditApiService>();
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IGoogleIdTokenValidator, BetsTrading.Infrastructure.Services.GoogleIdTokenValidator>();
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.ICoinPurchasePricingService, BetsTrading.Infrastructure.Services.CoinPurchasePricingService>();
builder.Services.AddScoped<BetsTrading.Application.Interfaces.IStripePaymentFulfillmentService, BetsTrading.Infrastructure.Services.StripePaymentFulfillmentService>();

// Localization Service
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.ILocalizationService, BetsTrading.Infrastructure.Services.LocalizationService>();

// IP Geo Service (ip-api.com, igual que legacy GetGeoLocationFromIp)
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IIpGeoService, BetsTrading.Infrastructure.Services.IpGeoService>();

// Max odds por ticker/timeframe en memoria (EUR y USD)
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.ITickerMaxOddsService, BetsTrading.Infrastructure.Services.TickerMaxOddsService>();

// Updater Service - Necesita DbContext para BulkExtensions
builder.Services.AddScoped<BetsTrading.Application.Interfaces.IUpdaterService>(sp =>
{
    var unitOfWork = sp.GetRequiredService<BetsTrading.Domain.Interfaces.IUnitOfWork>();
    var logger = sp.GetRequiredService<BetsTrading.Application.Interfaces.IApplicationLogger>();
    var dbContext = sp.GetRequiredService<BetsTrading.Infrastructure.Persistence.AppDbContext>();
    var tickerMaxOddsService = sp.GetRequiredService<BetsTrading.Application.Interfaces.ITickerMaxOddsService>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new BetsTrading.Infrastructure.Services.UpdaterService(unitOfWork, logger, dbContext, tickerMaxOddsService, httpClientFactory);
});

// Odds Adjuster Options
builder.Services.Configure<BetsTrading.Infrastructure.HostedServices.OddsAdjusterOptions>(options =>
{
    // El valor por defecto ya está configurado en el record (4 segundos)
    // Si necesitas cambiarlo, puedes hacerlo desde appsettings.json
});

// Admin panel: configuración en tiempo real (panel secreto en /status)
builder.Services.AddSingleton<BetsTrading.Infrastructure.Services.AdminRuntimeConfig>();
builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IAdminRuntimeConfig>(sp =>
    sp.GetRequiredService<BetsTrading.Infrastructure.Services.AdminRuntimeConfig>());
builder.Services.AddScoped<BetsTrading.Infrastructure.Services.AccessLockService>();

builder.Services.AddSingleton<IJwtTokenService>(sp =>
    new JwtTokenService(
        localIssuer,
        localAudience,
        jwtLocalKey,
        sp.GetRequiredService<BetsTrading.Application.Interfaces.IAdminRuntimeConfig>()));

// Hosted Services (odds se actualizan al hacer NewBet, no con job periódico)
builder.Services.AddHostedService<BetsTrading.Infrastructure.HostedServices.UpdaterHostedService>();
builder.Services.AddHostedService<BetsTrading.Infrastructure.HostedServices.RaffleDrawHostedService>();
builder.Services.AddScoped<BetsTrading.Application.Interfaces.IRaffleDrawService, BetsTrading.Infrastructure.Services.RaffleDrawService>();
builder.Services.AddScoped<BetsTrading.Application.Interfaces.IRaffleAdminService, BetsTrading.Infrastructure.Services.RaffleAdminService>();
builder.Services.AddScoped<BetsTrading.Application.Interfaces.IUserAccountDeletionService, BetsTrading.Infrastructure.Services.UserAccountDeletionService>();

builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IEmailService>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BetsTrading.Infrastructure.Services.SmtpSettings>>().Value;
    return new BetsTrading.Infrastructure.Services.EmailService(settings);
});

// Firebase Notification Service
builder.Services.Configure<BetsTrading.Infrastructure.Services.FirebaseSettings>(settings =>
{
    settings.CredentialsPath = builder.Configuration["Firebase:CredentialsPath"] ?? "betrader-v1-firebase.json";
});

builder.Services.AddSingleton<BetsTrading.Application.Interfaces.IFirebaseNotificationService>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BetsTrading.Infrastructure.Services.FirebaseSettings>>().Value;
    var logger = sp.GetRequiredService<BetsTrading.Application.Interfaces.IApplicationLogger>();
    return new BetsTrading.Infrastructure.Services.FirebaseNotificationService(settings, logger);
});

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IStepUpTokenService>(sp =>
    new StepUpTokenService(
        sp.GetRequiredService<IMemoryCache>(),
        localIssuer,
        jwtLocalKey));
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Stripe Configuration
var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") 
    ?? builder.Configuration["Stripe:SecretKey"] 
    ?? "";
if (!string.IsNullOrEmpty(stripeSecretKey))
{
    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
    customLogger.Log.Information("[PROGRAM] :: Stripe API Key configured.");
}
else
{
    customLogger.Log.Warning("[PROGRAM] :: Stripe API Key not configured.");
}

var app = builder.Build();

// [EMAIL_FLOW] Diagnóstico SMTP al arranque (temporal): comprobar si .env se carga — Debug para no saturar
try
{
    var smtp = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BetsTrading.Infrastructure.Services.SmtpSettings>>().Value;
    customLogger.Log.Debug("[EMAIL_FLOW] SMTP at startup: HostLen={0}, FromLen={1}, PwdLen={2}, UserLen={3}",
        string.IsNullOrEmpty(smtp.Host) ? 0 : smtp.Host.Length,
        string.IsNullOrEmpty(smtp.FromAddress) ? 0 : smtp.FromAddress.Length,
        string.IsNullOrEmpty(smtp.Password) ? 0 : smtp.Password.Length,
        string.IsNullOrEmpty(smtp.Username) ? 0 : smtp.Username.Length);
}
catch (Exception ex)
{
    customLogger.Log.Debug("[EMAIL_FLOW] No se pudo loguear diagnóstico SMTP: {0}", ex.Message);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Exception handling primero: cualquier excepción no controlada devuelve 500 con cuerpo JSON (evita respuesta vacía)
app.UseMiddleware<BetsTrading.API.Middleware.ExceptionHandlingMiddleware>();

// Middleware - Matching legacy Program.cs order exactly
// ForwardedHeaders DEBE ir primero para que otros middlewares vean el protocolo/esquema correcto
app.UseForwardedHeaders();

// Middleware para habilitar buffering del request body (antes de model binding)
// Esto permite que los controladores puedan leer el body múltiples veces si es necesario
app.Use(async (context, next) =>
{
    // Habilitar buffering solo para métodos que pueden tener body (POST, PUT, PATCH)
    if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH")
    {
        context.Request.EnableBuffering();
        // Establecer el límite del buffer si es necesario (por defecto es 30KB)
        context.Request.Body.Position = 0;
    }
    await next();
});

// Middleware de logging para TODAS las peticiones (antes de autenticación) - Debug para no saturar logs
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var method = context.Request.Method;
    var authHeader = context.Request.Headers["Authorization"].ToString();
    var hasAuth = !string.IsNullOrEmpty(authHeader);
    
    customLogger.Log.Debug("[MIDDLEWARE] :: Request :: Method: {method}, Path: {path}, HasAuth: {hasAuth}", 
        method, path, hasAuth);
    
    await next();
    
    var responseSize = context.Response.ContentLength ?? -1;
    var hasStarted = context.Response.HasStarted;
    customLogger.Log.Debug("[MIDDLEWARE] :: Response :: Method: {method}, Path: {path}, StatusCode: {statusCode}, ContentLength: {size}, HasStarted: {started}", 
        method, path, context.Response.StatusCode, responseSize, hasStarted);
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseIpRateLimiting();
app.UseResponseCompression();
app.UseMiddleware<BetsTrading.API.Middleware.AccessLockMiddleware>();

// Endpoint para servir el logo
app.MapGet("/logo", (IWebHostEnvironment env) =>
{
    // Intentar múltiples rutas posibles
    var logoPaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "betstrading_logo.png"),
        Path.Combine(env.ContentRootPath, "betstrading_logo.png"),
        Path.Combine(Directory.GetCurrentDirectory(), "betstrading_logo.png"),
        Path.Combine(AppContext.BaseDirectory, "BetsTrading.API", "betstrading_logo.png"),
        Path.Combine(env.ContentRootPath, "BetsTrading.API", "betstrading_logo.png")
    };

    foreach (var logoPath in logoPaths)
    {
        if (System.IO.File.Exists(logoPath))
        {
            var imageBytes = System.IO.File.ReadAllBytes(logoPath);
            return Results.File(imageBytes, "image/png");
        }
    }
    
    return Results.NotFound();
}).AllowAnonymous();

// Status y test - accesibles sin auth para load balancers y curl local
var adminSecret = Environment.GetEnvironmentVariable("ADMIN_SECRET") ?? "";
var adminSecretHash = string.IsNullOrEmpty(adminSecret)
    ? ""
    : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(adminSecret))).ToLowerInvariant();

app.MapGet("/status", () =>
{
    var html = StatusView.GetHtml(DateTime.UtcNow.ToString("o"), adminSecretHash);
    return Results.Content(html, "text/html; charset=utf-8");
}).AllowAnonymous();

app.MapGet("/status/delete-account", () =>
{
    var html = DeleteAccountView.GetHtml();
    return Results.Content(html, "text/html; charset=utf-8");
}).AllowAnonymous();

// Página pública para verificación OAuth (Google): descripción de la app, uso de datos, enlace a privacidad
app.MapGet("/info", (IConfiguration configuration) =>
{
    var privacyUrl = configuration["AppInfo:PrivacyPolicyUrl"]
        ?? Environment.GetEnvironmentVariable("APP_INFO_PRIVACY_POLICY_URL");
    var privacyUrlEs = configuration["AppInfo:PrivacyPolicyUrlEs"]
        ?? Environment.GetEnvironmentVariable("APP_INFO_PRIVACY_POLICY_URL_ES");
    var html = AppInfoView.GetHtml(privacyUrl, privacyUrlEs);
    return Results.Content(html, "text/html; charset=utf-8");
}).AllowAnonymous();

// Panel admin secreto: unlock con ADMIN_SECRET (cada letra seguida, sin espacios)
app.MapPost("/status/admin/unlock", async (HttpContext ctx) =>
{
    customLogger.Log.Debug("[ADMIN] :: Unlock attempt");
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
    var secret = json.TryGetProperty("secret", out var p) ? p.GetString() ?? "" : "";
    if (string.IsNullOrEmpty(adminSecret) || secret != adminSecret)
    {
        customLogger.Log.Warning("[ADMIN] :: Unlock failed: invalid or missing secret");
        return Results.Json(new { error = "Invalid secret" }, statusCode: 401);
    }

    var keyBytes = System.Text.Encoding.UTF8.GetBytes(jwtLocalKey);
    if (keyBytes.Length < 32)
    {
        customLogger.Log.Warning("[ADMIN] :: Unlock failed: JWT key too short ({Len} chars)", keyBytes.Length);
        return Results.Json(new { error = "JWT key too short" }, statusCode: 500);
    }
    var handler = new JwtSecurityTokenHandler();
    var now = DateTime.UtcNow;
    var token = handler.WriteToken(new JwtSecurityToken(
        issuer: localIssuer,
        audience: "bets-trading-admin",
        claims: new[] { new Claim("is_admin", "true"), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) },
        notBefore: now,
        expires: now.AddMinutes(5),
        signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));
    customLogger.Log.Information("[ADMIN] :: Unlock successful, token issued");
    return Results.Json(new { token });
}).AllowAnonymous();

// GET config (requiere Bearer token admin)
app.MapGet("/status/admin/config", (HttpContext ctx, BetsTrading.Infrastructure.Services.AdminRuntimeConfig adminConfig, IWebHostEnvironment env) =>
{
    customLogger.Log.Debug("[ADMIN] :: GET config request");
    if (!AdminAuthHelper.TryValidateAdminToken(ctx, jwtLocalKey, localIssuer, out _))
    {
        customLogger.Log.Warning("[ADMIN] :: GET config: Unauthorized (invalid or expired token)");
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
    }

    string? eurJson = null, usdJson = null;
    foreach (var currency in new[] { "eur", "usd" })
    {
        var path = Path.Combine(AppContext.BaseDirectory ?? "", $"exchange_options_{currency}.json");
        if (!System.IO.File.Exists(path)) path = Path.Combine(env.ContentRootPath, $"exchange_options_{currency}.json");
        if (System.IO.File.Exists(path)) try { (currency == "eur" ? ref eurJson : ref usdJson) = System.IO.File.ReadAllText(path); } catch { }
    }
    var dto = adminConfig.ToDto(
        adminConfig.ExchangeOptionsEur ?? eurJson,
        adminConfig.ExchangeOptionsUsd ?? usdJson);
    return Results.Json(dto);
}).AllowAnonymous();

// Bloqueo global de acceso (toggle + invalidación de sesiones + FCM logout al activar)
app.MapPost("/status/admin/access-lock", async (
    HttpContext ctx,
    BetsTrading.Infrastructure.Services.AccessLockService accessLockService) =>
{
    if (!AdminAuthHelper.TryValidateAdminToken(ctx, jwtLocalKey, localIssuer, out _))
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

    BetsTrading.Infrastructure.Services.AccessLockRequestDto? body;
    try
    {
        body = await ctx.Request.ReadFromJsonAsync<BetsTrading.Infrastructure.Services.AccessLockRequestDto>();
    }
    catch
    {
        return Results.Json(new { error = "Invalid JSON" }, statusCode: 400);
    }

    if (body == null)
        return Results.Json(new { error = "Invalid body" }, statusCode: 400);

    var result = await accessLockService.SetAccessLockAsync(body.Enabled);

    return Results.Json(new
    {
        accessLockEnabled = result.AccessLockEnabled,
        sessionsInvalidated = result.SessionsInvalidated,
        logoutNotificationsSent = result.LogoutNotificationsSent,
    });
}).AllowAnonymous();

// POST config (requiere Bearer token admin)
app.MapPost("/status/admin/config", async (HttpContext ctx, BetsTrading.Infrastructure.Services.AdminRuntimeConfig adminConfig, IWebHostEnvironment env) =>
{
    if (!AdminAuthHelper.TryValidateAdminToken(ctx, jwtLocalKey, localIssuer, out _))
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

    BetsTrading.Infrastructure.Services.AdminConfigDto? dto;
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        dto = System.Text.Json.JsonSerializer.Deserialize<BetsTrading.Infrastructure.Services.AdminConfigDto>(body, opts);
    }
    catch { return Results.Json(new { error = "Invalid JSON" }, statusCode: 400); }
    if (dto == null) return Results.Json(new { error = "Invalid body" }, statusCode: 400);

    if (dto.JwtTokenExpirationHours < 1 || dto.JwtTokenExpirationHours > 720)
    {
        customLogger.Log.Warning("[ADMIN] :: POST config: JwtTokenExpirationHours fuera de rango: {0}", dto.JwtTokenExpirationHours);
        return Results.Json(new { error = "jwtTokenExpirationHours debe estar entre 1 y 720 (horas)." }, statusCode: 400);
    }

    const int maxRegistrationFavIds = 50;
    if (dto.RegistrationDefaultFavoriteAssetIds != null &&
        dto.RegistrationDefaultFavoriteAssetIds.Length > maxRegistrationFavIds)
    {
        customLogger.Log.Warning("[ADMIN] :: POST config: demasiados registrationDefaultFavoriteAssetIds: {0}", dto.RegistrationDefaultFavoriteAssetIds.Length);
        return Results.Json(new { error = $"registrationDefaultFavoriteAssetIds: máximo {maxRegistrationFavIds} ids." }, statusCode: 400);
    }

    if (dto.MandatoryAdForegroundMinutes < 1 || dto.MandatoryAdForegroundMinutes > 480)
    {
        customLogger.Log.Warning("[ADMIN] :: POST config: mandatoryAdForegroundMinutes fuera de rango: {0}", dto.MandatoryAdForegroundMinutes);
        return Results.Json(new { error = "mandatoryAdForegroundMinutes debe estar entre 1 y 480 (minutos en primer plano)." }, statusCode: 400);
    }

    if (dto.MandatoryAdCooldownSeconds < 30 || dto.MandatoryAdCooldownSeconds > 86400)
    {
        customLogger.Log.Warning("[ADMIN] :: POST config: mandatoryAdCooldownSeconds fuera de rango: {0}", dto.MandatoryAdCooldownSeconds);
        return Results.Json(new { error = "mandatoryAdCooldownSeconds debe estar entre 30 y 86400 (segundos)." }, statusCode: 400);
    }

    if (dto.NoAdsPriceEur < 0.5 || dto.NoAdsPriceEur > 999.99 || dto.NoAdsPriceUsd < 0.5 || dto.NoAdsPriceUsd > 999.99)
    {
        customLogger.Log.Warning("[ADMIN] :: POST config: precio No Ads inválido EUR={Eur} USD={Usd}", dto.NoAdsPriceEur, dto.NoAdsPriceUsd);
        return Results.Json(new { error = "noAdsPriceEur y noAdsPriceUsd deben estar entre 0.5 y 999.99." }, statusCode: 400);
    }

    // Validar que el JSON de exchange options es válido antes de guardar (evitar petar StoreOptions/RetireBalance)
    var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (!string.IsNullOrWhiteSpace(dto.ExchangeOptionsEur))
    {
        try
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<List<BetsTrading.Application.DTOs.StoreOptionDto>>(dto.ExchangeOptionsEur, jsonOpts);
        }
        catch (System.Text.Json.JsonException ex)
        {
            customLogger.Log.Warning("[ADMIN] :: POST config: ExchangeOptionsEur JSON inválido: {0}", ex.Message);
            return Results.Json(new { error = "Exchange options EUR: JSON inválido. Revisa la sintaxis.", detail = ex.Message }, statusCode: 400);
        }
    }
    if (!string.IsNullOrWhiteSpace(dto.ExchangeOptionsUsd))
    {
        try
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<List<BetsTrading.Application.DTOs.StoreOptionDto>>(dto.ExchangeOptionsUsd, jsonOpts);
        }
        catch (System.Text.Json.JsonException ex)
        {
            customLogger.Log.Warning("[ADMIN] :: POST config: ExchangeOptionsUsd JSON inválido: {0}", ex.Message);
            return Results.Json(new { error = "Exchange options USD: JSON inválido. Revisa la sintaxis.", detail = ex.Message }, statusCode: 400);
        }
    }

    adminConfig.SetFromDto(dto);

    var nowUtc = DateTime.UtcNow;
    var configuredMinute = Math.Clamp(dto.UpdaterMinute, 0, 59);
    var currentHourAtConfiguredMinuteUtc = new DateTime(
        nowUtc.Year,
        nowUtc.Month,
        nowUtc.Day,
        nowUtc.Hour,
        configuredMinute,
        0,
        DateTimeKind.Utc);
    var toleranceEndUtc = currentHourAtConfiguredMinuteUtc.AddSeconds(59);
    var nextUpdaterRunUtc = nowUtc <= currentHourAtConfiguredMinuteUtc
        ? currentHourAtConfiguredMinuteUtc
        : (nowUtc <= toleranceEndUtc ? nowUtc : currentHourAtConfiguredMinuteUtc.AddHours(1));
    var waitSeconds = Math.Max(0, (nextUpdaterRunUtc - nowUtc).TotalSeconds);

    // Persistir exchange options a archivo si existe la ruta
    var baseDir = AppContext.BaseDirectory ?? env.ContentRootPath;
    if (!string.IsNullOrWhiteSpace(dto.ExchangeOptionsEur))
        try { await System.IO.File.WriteAllTextAsync(Path.Combine(baseDir, "exchange_options_eur.json"), dto.ExchangeOptionsEur); } catch { }
    if (!string.IsNullOrWhiteSpace(dto.ExchangeOptionsUsd))
        try { await System.IO.File.WriteAllTextAsync(Path.Combine(baseDir, "exchange_options_usd.json"), dto.ExchangeOptionsUsd); } catch { }

    customLogger.Log.Information(
        "[ADMIN] :: POST config: OK (config updated) | updater_minute={UpdaterMinute} | now_utc={NowUtc:O} | now_local={NowLocal:O} | next_updater_run_utc={NextRunUtc:O} | wait_seconds={WaitSeconds:F0}",
        configuredMinute,
        nowUtc,
        DateTimeOffset.Now,
        nextUpdaterRunUtc,
        waitSeconds);
    return Results.Ok();
}).AllowAnonymous();

// GET/POST sorteos (panel admin, 6 items en PostgreSQL)
app.MapGet("/status/admin/raffle-items", async (HttpContext ctx, BetsTrading.Application.Interfaces.IRaffleAdminService raffleAdmin) =>
{
    if (!AdminAuthHelper.TryValidateAdminToken(ctx, jwtLocalKey, localIssuer, out _))
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

    try
    {
        var items = await raffleAdmin.GetItemsAsync();
        return Results.Json(items);
    }
    catch (Exception ex)
    {
        customLogger.Log.Error(ex, "[ADMIN] :: GET raffle-items failed");
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
}).AllowAnonymous();

app.MapPost("/status/admin/raffle-items", async (HttpContext ctx, BetsTrading.Application.Interfaces.IRaffleAdminService raffleAdmin) =>
{
    if (!AdminAuthHelper.TryValidateAdminToken(ctx, jwtLocalKey, localIssuer, out _))
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);

    BetsTrading.Application.DTOs.SaveAdminRaffleItemsRequest? body;
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var raw = await reader.ReadToEndAsync();
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        body = System.Text.Json.JsonSerializer.Deserialize<BetsTrading.Application.DTOs.SaveAdminRaffleItemsRequest>(raw, opts);
    }
    catch
    {
        return Results.Json(new { error = "Invalid JSON" }, statusCode: 400);
    }

    if (body?.Items == null || body.Items.Count == 0)
        return Results.Json(new { error = "Items required" }, statusCode: 400);

    try
    {
        await raffleAdmin.SaveItemsAsync(body.Items);
        customLogger.Log.Information("[ADMIN] :: POST raffle-items: OK ({Count} items)", body.Items.Count);
        return Results.Ok();
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 400);
    }
    catch (Exception ex)
    {
        customLogger.Log.Error(ex, "[ADMIN] :: POST raffle-items failed");
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
}).AllowAnonymous();

// Borrado de cuenta (público): el usuario confirma con su contraseña y nombre completo
app.MapPost("/status/delete-account", async (
    HttpContext ctx,
    BetsTrading.Application.Interfaces.IUserAccountDeletionService deletionService) =>
{
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var raw = await reader.ReadToEndAsync();
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = System.Text.Json.JsonSerializer.Deserialize<DeleteAccountRequestDto>(raw, opts);
        if (body == null)
            return Results.Json(new { error = "Invalid body" }, statusCode: 400);

        var result = await deletionService.DeleteAccountAsync(
            body.Username ?? string.Empty,
            body.Password ?? string.Empty,
            body.ConfirmFullname ?? string.Empty);

        if (!result.Success)
        {
            customLogger.Log.Warning("[DeleteAccount] :: Public delete failed: {Message}", result.Message);
            return Results.Json(new { success = false, error = result.Message }, statusCode: 400);
        }

        customLogger.Log.Information("[DeleteAccount] :: Public delete OK userId={UserId}", result.UserId);
        return Results.Json(new { success = true, message = result.Message });
    }
    catch (Exception ex)
    {
        customLogger.Log.Error(ex, "[DeleteAccount] :: Public delete exception");
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
}).AllowAnonymous();

// Endpoint JSON para health checks programáticos (load balancers, monitoreo)
app.MapHealthChecks("/health/json").AllowAnonymous();
app.MapGet("/test", () => "OK").AllowAnonymous();

// HTTPS redirect solo en Development. En Production (Nginx) el proxy termina SSL;
// la app solo recibe HTTP en localhost:5000, así curl http://localhost:5000/health responde.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

customLogger.Log.Information("[PROGRAM] :: All Backend services started successfully!");
customLogger.Log.Information("[PROGRAM] :: API is ready to accept requests. Hosted services will start their work shortly.");

// Apply migrations in background (non-blocking)
_ = Task.Run(async () =>
{
    try
    {
        // Wait 5 seconds for the API to be completely ready
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();
        
        // Verificar si hay migraciones pendientes antes de ejecutar Migrate()
        var pendingMigrations = context.Database.GetPendingMigrations().ToList();
        
        if (pendingMigrations.Any())
        {
            customLogger.Log.Information("[PROGRAM] :: Applying {Count} pending Entity Framework Core migrations in background...", pendingMigrations.Count);
            foreach (var migration in pendingMigrations)
            {
                customLogger.Log.Information("[PROGRAM] :: Applying migration: {Migration}", migration);
            }
            context.Database.Migrate();
            customLogger.Log.Information("[PROGRAM] :: Migrations applied successfully.");
        }
        else
        {
            customLogger.Log.Information("[PROGRAM] :: No pending migrations. Database is up to date.");
        }
    }
    catch (Exception ex)
    {
        customLogger.Log.Error(ex, "[PROGRAM] :: An error occurred while verifying/applying database migrations. Error: {Error}", ex.Message);
        customLogger.Log.Error(ex, "[PROGRAM] :: Stack trace: {StackTrace}", ex.StackTrace ?? "No stack trace");
        // Don't throw exception so the application can continue
    }
}).ContinueWith(task =>
{
    if (task.IsFaulted && task.Exception != null)
    {
        customLogger.Log.Error(task.Exception, "[PROGRAM] :: Background migration task failed with unhandled exception.");
    }
}, TaskContinuationOptions.OnlyOnFaulted);

try
{
    customLogger.Log.Information("[PROGRAM] :: Starting application...");
    app.Run();
}
catch (Exception ex)
{
    customLogger.Log.Fatal(ex, "[PROGRAM] :: Application crashed with unhandled exception: {Error}", ex.Message);
    customLogger.Log.Fatal(ex, "[PROGRAM] :: Stack trace: {StackTrace}", ex.StackTrace ?? "No stack trace");
    throw; // Re-throw to let systemd handle it
}
}
catch (Exception ex)
{
    // Fallback logger in case customLogger is not available
    Console.WriteLine($"[FATAL] Application failed to start: {ex.Message}");
    Console.WriteLine($"[FATAL] Stack trace: {ex.StackTrace}");
    throw;
}
