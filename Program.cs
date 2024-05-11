using BetsTrading_Service.Controllers;
using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Net;

public class Program
{
  public static void Main(string[] args)
  {
    var logPath = "Logs/BetsTrading_Service_.log";
    RollingInterval loggingInterval = RollingInterval.Day;

    ICustomLogger customLogger = new CustomLogger(new LoggerConfiguration()
        .MinimumLevel.Debug()
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

      
      builder.Services.AddControllers();
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();
      builder.Services.AddTransient<AuthController>();
      builder.Services.AddTransient<InfoController>();
      builder.Services.AddTransient<FinancialAssetsController>();
      builder.Services.AddHsts(options =>
      {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(60);
        customLogger.Log.Information("[PROGRAM] :: Hsts max duration (days) : {d}", options.MaxAge.TotalSeconds/(3600*24));
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

      // Configuración de middleware en ambiente de desarrollo
      if (app.Environment.IsDevelopment())
      {
        customLogger.Log.Warning("[PROGRAM] :: Developer mode activated! Using SwaggerUI and Hsts");
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseHsts();
      }

      // Configuración de middleware común
      app.UseResponseCompression();
      app.UseHttpsRedirection();
      app.UseAuthorization();
      app.MapControllers();
      customLogger.Log.Information("[PROGRAM] :: Controller endpoints added succesfully");

      // Log final para marcar el inicio de la API
      customLogger.Log.Information("[PROGRAM] :: Backend service started succesfully!");

    
      app.Run();
    }
    catch (Exception ex)
    {
      customLogger.Log.Error(ex, "[PROGRAM] :: Service terminated unexpectedly: {ErrorMessage}", ex.Message);
      throw;  
    }    
  }
}
