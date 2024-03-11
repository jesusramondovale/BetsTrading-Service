using BetsTrading_Service.Database;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Serilog;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.File("./Logs/main.log")
    .CreateLogger();

try
{
  // Database connection settings (hosted locally on pre-alpha versions)
  var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

  // DbContext configuration
  builder.Services.AddDbContext<AppDbContext>(options =>
      options.UseNpgsql(connectionString));

  // API Service controllers
  builder.Services.AddControllers();
  builder.Services.AddSwaggerGen();

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

  if (app.Environment.IsDevelopment())
  {
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHsts();
  }

  app.UseResponseCompression();
  app.UseHttpsRedirection();
  app.UseAuthorization();
  app.MapControllers();

  Log.Information("API Service started");
  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "API Service terminated unexpectedly. Exception elevated to main process: ", ex.Message);
  return;
}
finally
{
  Log.CloseAndFlush();
}
