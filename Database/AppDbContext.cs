namespace BetsTrading_Service.Database
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
    public DbSet<Bet> Bet{ get; set; }
    public DbSet<FinancialAsset> FinancialAssets { get; set; }
    public DbSet<Trend> Trends { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<BetZone> BetZones { get; set; }
    public DbSet<PriceBet> PriceBets { get; set; }
    public DbSet<WithdrawalMethod> WithdrawalMethods{ get; set; }
    public DbSet<RewardNonce> RewardNonces{ get; set; }
    public DbSet<RewardTransaction> RewardTransactions{ get; set; }
    public DbSet<PaymentData> PaymentData { get; set; }
    public DbSet<WithdrawalData> WithdrawalData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // Especifica el esquema "BetsTrading" para todas las tablas del modelo
      modelBuilder.HasDefaultSchema("BetsTrading");

      modelBuilder.Entity<Bet>(entity =>
      {
        entity.HasKey(e => e.id);
        entity.Property(e => e.ticker).IsRequired();
      });

      modelBuilder.Entity<FinancialAsset>(entity =>
      {
        entity.HasKey(e => e.ticker);
        entity.Property(e => e.name).IsRequired();
      });

      modelBuilder
        .Entity<BetZone>()
        .Property(e => e.start_date)
        .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        .HasColumnType("timestamp without time zone");

      modelBuilder
          .Entity<BetZone>()
          .Property(e => e.end_date)
          .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
          .HasColumnType("timestamp without time zone");


      base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      optionsBuilder.UseLoggerFactory(_loggerFactory);
      base.OnConfiguring(optionsBuilder);
    }
  }
}

