using BetsTrading_Service.Database;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Serilog;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore;

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

  var kestrelConfig = builder.Configuration.GetSection("Kestrel");

  var app = builder.Build();
  

  if (app.Environment.IsDevelopment())
  {
    app.UseSwagger();
    app.UseSwaggerUI();
  }

  //app.UseHttpsRedirection();
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

