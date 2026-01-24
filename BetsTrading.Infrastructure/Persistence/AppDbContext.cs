using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;

namespace BetsTrading.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Bet> Bets { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<BetZone> BetZones { get; set; }
    public DbSet<FinancialAsset> FinancialAssets { get; set; }
    public DbSet<VerificationCode> VerificationCodes { get; set; }
    public DbSet<PaymentData> PaymentData { get; set; }
    public DbSet<WithdrawalData> WithdrawalData { get; set; }
    public DbSet<RewardNonce> RewardNonces { get; set; }
    public DbSet<RewardTransaction> RewardTransactions { get; set; }
    public DbSet<AssetCandle> AssetCandles { get; set; }
    public DbSet<AssetCandleUSD> AssetCandlesUSD { get; set; }
    public DbSet<Trend> Trends { get; set; }
    public DbSet<BetZoneUSD> BetZonesUSD { get; set; }
    public DbSet<PriceBet> PriceBets { get; set; }
    public DbSet<PriceBetUSD> PriceBetsUSD { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Raffle> Raffles { get; set; }
    public DbSet<RaffleItem> RaffleItems { get; set; }
    public DbSet<WithdrawalMethod> WithdrawalMethods { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("BetsTrading");

        // Configuración de Bet
        modelBuilder.Entity<Bet>(entity =>
        {
            entity.ToTable("Bets", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.BetAmount).HasColumnName("bet_amount");
            entity.Property(e => e.OriginValue).HasColumnName("origin_value");
            entity.Property(e => e.OriginOdds).HasColumnName("origin_odds");
            // target_value y target_margin no existen en la base de datos, se obtienen de BetZone
            entity.Ignore(e => e.TargetValue);
            entity.Ignore(e => e.TargetMargin);
            entity.Property(e => e.TargetWon).HasColumnName("target_won");
            entity.Property(e => e.Finished).HasColumnName("finished");
            entity.Property(e => e.Paid).HasColumnName("paid");
            // bet_zone puede referenciar tanto BetZones como BetZonesUSD según la moneda
            // No configuramos relación de clave foránea para permitir ambas tablas
            entity.Property(e => e.BetZoneId).HasColumnName("bet_zone");
            entity.Property(e => e.Archived).HasColumnName("archived");
        });

        // Configuración de User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Fcm).HasColumnName("fcm");
            entity.Property(e => e.Fullname).HasColumnName("fullname");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.Gender).HasColumnName("gender");
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.Birthday).HasColumnName("birthday");
            entity.Property(e => e.SigninDate).HasColumnName("signin_date");
            entity.Property(e => e.LastSession).HasColumnName("last_session");
            entity.Property(e => e.Username).HasColumnName("username").IsRequired();
            entity.Property(e => e.TokenExpiration).HasColumnName("token_expiration");
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.DiditSessionId).HasColumnName("didit_session_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.FailedAttempts).HasColumnName("failed_attempts");
            entity.Property(e => e.LastLoginAttempt).HasColumnName("last_login_attempt");
            entity.Property(e => e.ProfilePic).HasColumnName("profile_pic");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.PendingBalance).HasColumnName("pending_balance");
            entity.Property(e => e.CreditCard).HasColumnName("credit_card");
        });

        // Configuración de BetZone
        modelBuilder.Entity<BetZone>(entity =>
        {
            entity.ToTable("BetZones", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.TargetValue).HasColumnName("target_value");
            entity.Property(e => e.BetMargin).HasColumnName("bet_margin");
            entity.Property(e => e.StartDate)
                .HasColumnName("start_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndDate)
                .HasColumnName("end_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.TargetOdds).HasColumnName("target_odds");
            entity.Property(e => e.BetType).HasColumnName("bet_type");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Timeframe).HasColumnName("timeframe");
        });

        // Configuración de FinancialAsset
        modelBuilder.Entity<FinancialAsset>(entity =>
        {
            entity.ToTable("FinancialAssets", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Group).HasColumnName("group").IsRequired();
            entity.Property(e => e.Icon).HasColumnName("icon");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.CurrentEur).HasColumnName("current_eur");
            entity.Property(e => e.CurrentUsd).HasColumnName("current_usd");
            entity.Property(e => e.CurrentMaxOdd).HasColumnName("current_max_odd");
            entity.Property(e => e.CurrentMaxOddDirection).HasColumnName("current_max_odd_direction");
            entity.HasIndex(e => e.Ticker).IsUnique();
        });

        // Configuración de VerificationCode
        modelBuilder.Entity<VerificationCode>(entity =>
        {
            entity.ToTable("VerificationCodes", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.Code).HasColumnName("code").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Verified).HasColumnName("verified");
        });

        // Configuración de PaymentData
        modelBuilder.Entity<PaymentData>(entity =>
        {
            entity.ToTable("PaymentData", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.PaymentIntentId).HasColumnName("payment_intent_id").IsRequired();
            entity.Property(e => e.Coins).HasColumnName("coins");
            entity.Property(e => e.Currency).HasColumnName("currency").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ExecutedAt).HasColumnName("executed_at");
            entity.Property(e => e.IsPaid).HasColumnName("is_paid");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").IsRequired();
        });

        // Configuración de WithdrawalData
        modelBuilder.Entity<WithdrawalData>(entity =>
        {
            entity.ToTable("WithdrawalData", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Coins).HasColumnName("coins");
            entity.Property(e => e.Currency).HasColumnName("currency").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ExecutedAt).HasColumnName("executed_at");
            entity.Property(e => e.IsPaid).HasColumnName("is_paid");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method").IsRequired();
        });

        // Configuración de RewardNonce — columnas en PascalCase (tabla creada por DbContext legacy sin HasColumnName)
        modelBuilder.Entity<RewardNonce>(entity =>
        {
            entity.ToTable("RewardNonces", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Nonce).HasColumnName("Nonce").IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired().HasMaxLength(128);
            entity.Property(e => e.AdUnitId).HasColumnName("AdUnitId").IsRequired().HasMaxLength(128);
            entity.Property(e => e.Purpose).HasColumnName("Purpose").HasMaxLength(64);
            entity.Property(e => e.Coins).HasColumnName("Coins");
            entity.Property(e => e.Used).HasColumnName("Used");
            entity.Property(e => e.ExpiresAt).HasColumnName("ExpiresAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UsedAt).HasColumnName("UsedAt");
            entity.HasIndex(e => e.Nonce).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // Configuración de RewardTransaction
        modelBuilder.Entity<RewardTransaction>(entity =>
        {
            entity.ToTable("RewardTransactions", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id").IsRequired().HasMaxLength(128);
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(128);
            entity.Property(e => e.Coins).HasColumnName("coins").HasColumnType("decimal(18,2)");
            entity.Property(e => e.AdUnitId).HasColumnName("ad_unit_id").HasMaxLength(128);
            entity.Property(e => e.RewardItem).HasColumnName("reward_item").HasMaxLength(64);
            entity.Property(e => e.RewardAmountRaw).HasColumnName("reward_amount_raw");
            entity.Property(e => e.SsvKeyId).HasColumnName("ssv_key_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.RawQuery).HasColumnName("raw_query");
            entity.HasIndex(e => e.TransactionId).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // Configuración de AssetCandle
        modelBuilder.Entity<AssetCandle>(entity =>
        {
            entity.ToTable("AssetCandles", "BetsTrading");
            entity.HasKey(e => new { e.AssetId, e.Exchange, e.Interval, e.DateTime });
            entity.Property(e => e.AssetId).HasColumnName("AssetId");
            entity.Property(e => e.Exchange).HasColumnName("exchange");
            entity.Property(e => e.Interval).HasColumnName("interval");
            entity.Property(e => e.DateTime).HasColumnName("datetime").HasColumnType("timestamp without time zone");
            entity.Property(e => e.Open).HasColumnName("open");
            entity.Property(e => e.High).HasColumnName("high");
            entity.Property(e => e.Low).HasColumnName("low");
            entity.Property(e => e.Close).HasColumnName("close");
        });

        // Configuración de AssetCandleUSD
        modelBuilder.Entity<AssetCandleUSD>(entity =>
        {
            entity.ToTable("AssetCandlesUSD", "BetsTrading");
            entity.HasKey(e => new { e.AssetId, e.Exchange, e.Interval, e.DateTime });
            entity.Property(e => e.AssetId).HasColumnName("AssetId");
            entity.Property(e => e.Exchange).HasColumnName("exchange");
            entity.Property(e => e.Interval).HasColumnName("interval");
            entity.Property(e => e.DateTime).HasColumnName("datetime").HasColumnType("timestamp without time zone");
            entity.Property(e => e.Open).HasColumnName("open");
            entity.Property(e => e.High).HasColumnName("high");
            entity.Property(e => e.Low).HasColumnName("low");
            entity.Property(e => e.Close).HasColumnName("close");
        });

        // Configuración de Trend
        modelBuilder.Entity<Trend>(entity =>
        {
            entity.ToTable("Trends", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DailyGain).HasColumnName("daily_gain");
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
        });

        // Configuración de BetZoneUSD
        modelBuilder.Entity<BetZoneUSD>(entity =>
        {
            entity.ToTable("BetZonesUSD", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.TargetValue).HasColumnName("target_value");
            entity.Property(e => e.BetMargin).HasColumnName("bet_margin");
            entity.Property(e => e.StartDate)
                .HasColumnName("start_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndDate)
                .HasColumnName("end_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.TargetOdds).HasColumnName("target_odds");
            entity.Property(e => e.BetType).HasColumnName("bet_type");
            entity.Property(e => e.Active).HasColumnName("active");
            entity.Property(e => e.Timeframe).HasColumnName("timeframe");
        });

        // Configuración de PriceBet
        modelBuilder.Entity<PriceBet>(entity =>
        {
            entity.ToTable("PriceBets", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.PriceBetValue).HasColumnName("price_bet");
            entity.Property(e => e.Margin).HasColumnName("margin");
            entity.Property(e => e.Paid).HasColumnName("paid");
            entity.Property(e => e.BetDate).HasColumnName("bet_date");
            entity.Property(e => e.EndDate)
                .HasColumnName("end_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Archived).HasColumnName("archived");
            entity.Property(e => e.Prize).HasColumnName("prize");
        });

        // Configuración de PriceBetUSD
        modelBuilder.Entity<PriceBetUSD>(entity =>
        {
            entity.ToTable("PriceBetsUSD", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
            entity.Property(e => e.PriceBetValue).HasColumnName("price_bet");
            entity.Property(e => e.Margin).HasColumnName("margin");
            entity.Property(e => e.Paid).HasColumnName("paid");
            entity.Property(e => e.BetDate).HasColumnName("bet_date");
            entity.Property(e => e.EndDate)
                .HasColumnName("end_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Archived).HasColumnName("archived");
            entity.Property(e => e.Prize).HasColumnName("prize");
        });

        // Configuración de Favorite
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.ToTable("Favorites", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Ticker).HasColumnName("ticker").IsRequired();
        });

        // Configuración de Raffle
        modelBuilder.Entity<Raffle>(entity =>
        {
            entity.ToTable("Raffles", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.RaffleDate).HasColumnName("raffle_date");
        });

        // Configuración de RaffleItem
        modelBuilder.Entity<RaffleItem>(entity =>
        {
            entity.ToTable("RaffleItems", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.ShortName).HasColumnName("short_name").IsRequired();
            entity.Property(e => e.Coins).HasColumnName("coins");
            entity.Property(e => e.RaffleDate).HasColumnName("raffle_date");
            entity.Property(e => e.Icon).HasColumnName("icon").IsRequired();
            entity.Property(e => e.Participants).HasColumnName("participants");
        });

        // Configuración de WithdrawalMethod
        modelBuilder.Entity<WithdrawalMethod>(entity =>
        {
            entity.ToTable("WithdrawalMethods", "BetsTrading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Label).HasColumnName("label").IsRequired();
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Verified).HasColumnName("verified");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        base.OnModelCreating(modelBuilder);
    }
}
