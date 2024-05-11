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

    ICustomLogger customLogger = new CustomLogger(new LoggerConfiguration()
        .MinimumLevel.Debug()

        .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
        .CreateLogger());

    try
    {      
      var builder = WebApplication.CreateBuilder(args);          

      builder.Services.AddSingleton(customLogger);      

      // Log inicial para marcar el inicio del servicio
      customLogger.Log.Information("****** STARTING BETSTRADING BACKEND SERVICE ******");

      // Configuración de la base de datos
      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
      builder.Services.AddDbContext<AppDbContext>(options =>
          options.UseNpgsql(connectionString));

      // Configuración de servicios y middleware
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
      });

      builder.Services.AddHttpsRedirection(options =>
      {
        options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
        options.HttpsPort = 5001;
      });

      builder.Services.AddResponseCompression(options =>
      {
        options.EnableForHttps = true;
      });

      var app = builder.Build();

      // Configuración de middleware en ambiente de desarrollo
      if (app.Environment.IsDevelopment())
      {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseHsts();
      }

      // Configuración de middleware común
      app.UseResponseCompression();
      app.UseHttpsRedirection();
      app.UseAuthorization();
      app.MapControllers();

      // Log final para marcar el inicio de la API
      customLogger.Log.Information("Service started!");

    
      app.Run();
    }
    catch (Exception ex)
    {
      customLogger.Log.Error(ex, "API Service terminated unexpectedly: {ErrorMessage}", ex.Message);
      throw;  
    }    
  }
}
