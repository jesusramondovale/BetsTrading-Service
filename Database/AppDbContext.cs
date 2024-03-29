﻿namespace BetsTrading_Service.Database
{
  using BetsTrading_Service.Models;
  using Microsoft.EntityFrameworkCore;
  using Serilog;

  public class AppDbContext : DbContext
  {
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
    {
      // Configurar Serilog
      Log.Logger = new LoggerConfiguration()
          .WriteTo.File("../Logs/app.log")
          .CreateLogger();
      builder.AddSerilog();
    });

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Especifica el esquema "BetsTrading" al definir el DbSet
    public DbSet<User> Users { get; set; }
    public DbSet<Bet> InvestmentData{ get; set; }
    public DbSet<FinancialAsset> FinancialAssets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // Especifica el esquema "BetsTrading" para todas las tablas del modelo
      modelBuilder.HasDefaultSchema("BetsTrading");
      base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseLoggerFactory(_loggerFactory);
      base.OnConfiguring(optionsBuilder);
    }
  }
}

