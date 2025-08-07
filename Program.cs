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
      customLogger.Log.Information("[PROGRAM] :: ****** STARTING BETSTRADING BACKEND SERVICE ******");

      var builder = WebApplication.CreateBuilder(args);
      builder.Services.AddSingleton(customLogger);

      customLogger.Log.Information("[PROGRAM] :: Serilog service started. Logging on {pth} with interval: {itr}", logPath, loggingInterval.ToString());

      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
      builder.Services.AddDbContext<AppDbContext>(options =>
          options.UseNpgsql(connectionString));

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
