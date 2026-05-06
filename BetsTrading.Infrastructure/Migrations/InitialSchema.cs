using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetsTrading.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("00000000000000_InitialSchema")]
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Users and remaining (base) ----
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""Users""
                (
                    id text COLLATE pg_catalog.""default"" NOT NULL,
                    fullname text COLLATE pg_catalog.""default"" NOT NULL,
                    password text COLLATE pg_catalog.""default"" NOT NULL,
                    country text COLLATE pg_catalog.""default"",
                    gender text COLLATE pg_catalog.""default"",
                    email text COLLATE pg_catalog.""default"" NOT NULL,
                    birthday date,
                    signin_date date NOT NULL,
                    last_session timestamp without time zone,
                    username text COLLATE pg_catalog.""default"" NOT NULL,
                    token_expiration timestamp without time zone,
                    is_active boolean DEFAULT true,
                    failed_attempts integer DEFAULT 0,
                    last_login_attempt timestamp without time zone,
                    last_password_change timestamp without time zone,
                    profile_pic text COLLATE pg_catalog.""default"",
                    points numeric,
                    fcm text COLLATE pg_catalog.""default"",
                    pending_balance numeric,
                    is_verified boolean NOT NULL,
                    didit_session_id text COLLATE pg_catalog.""default"",
                    ""private"" boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""Users_pkey"" PRIMARY KEY (id),
                    CONSTRAINT ""Users_email_key"" UNIQUE (email),
                    CONSTRAINT ""Users_username_key"" UNIQUE (username)
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""VerificationCodes_Id_seq"";
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""PriceBetsUSD_id_seq"";
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""VerificationCodes""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""VerificationCodes_Id_seq""'::regclass),
                    email character varying(255) COLLATE pg_catalog.""default"" NOT NULL,
                    code character varying(10) COLLATE pg_catalog.""default"" NOT NULL,
                    ""createdAt"" timestamp without time zone NOT NULL DEFAULT now(),
                    ""expiresAt"" timestamp without time zone NOT NULL DEFAULT (now() + '00:10:00'::interval),
                    verified boolean NOT NULL DEFAULT false,
                    CONSTRAINT ""VerificationCodes_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".""IX_VerificationCodes_Code"" ON ""BetsTrading"".""VerificationCodes"" USING btree (code COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".""IX_VerificationCodes_Email"" ON ""BetsTrading"".""VerificationCodes"" USING btree (email COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""CopyTradingSubscriptions""
                (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    follower_user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    target_user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    copy_percent numeric(8,2) NOT NULL DEFAULT 50,
                    auto_adjust_by_balance boolean NOT NULL DEFAULT false,
                    stop_after_one_loss boolean NOT NULL DEFAULT false,
                    is_active boolean NOT NULL DEFAULT true,
                    created_at timestamp with time zone NOT NULL DEFAULT now(),
                    updated_at timestamp with time zone NOT NULL DEFAULT now(),
                    last_copied_at timestamp with time zone,
                    stopped_at timestamp with time zone,
                    stop_reason text COLLATE pg_catalog.""default"",
                    CONSTRAINT ""CopyTradingSubscriptions_pkey"" PRIMARY KEY (id),
                    CONSTRAINT ""CopyTradingSubscriptions_follower_fk"" FOREIGN KEY (follower_user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE CASCADE
                        ON DELETE CASCADE,
                    CONSTRAINT ""CopyTradingSubscriptions_target_fk"" FOREIGN KEY (target_user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE CASCADE
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""BetsTrading"".ux_copytrading_follower_target ON ""BetsTrading"".""CopyTradingSubscriptions"" USING btree (follower_user_id COLLATE pg_catalog.""default"", target_user_id COLLATE pg_catalog.""default"") TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".ix_copytrading_target_active ON ""BetsTrading"".""CopyTradingSubscriptions"" USING btree (target_user_id COLLATE pg_catalog.""default"", is_active) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""RaffleItems""
                (
                    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
                    name text COLLATE pg_catalog.""default"" NOT NULL,
                    short_name text COLLATE pg_catalog.""default"" NOT NULL,
                    coins integer NOT NULL,
                    raffle_date timestamp with time zone NOT NULL,
                    icon text COLLATE pg_catalog.""default"" NOT NULL,
                    participants integer,
                    CONSTRAINT ""RaffleItems_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".idx_raffleitems_raffle_date ON ""BetsTrading"".""RaffleItems"" USING btree (raffle_date ASC NULLS LAST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".idx_raffleitems_short_name ON ""BetsTrading"".""RaffleItems"" USING btree (short_name COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""Raffles""
                (
                    id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    raffle_date timestamp with time zone NOT NULL,
                    item_id text COLLATE pg_catalog.""default"" NOT NULL,
                    CONSTRAINT ""Raffles_pkey"" PRIMARY KEY (id),
                    CONSTRAINT fk_raffles_user FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE CASCADE
                        ON DELETE RESTRICT
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".idx_raffles_raffledate ON ""BetsTrading"".""Raffles"" USING btree (raffle_date ASC NULLS LAST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".idx_raffles_user_id ON ""BetsTrading"".""Raffles"" USING btree (user_id COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""RewardNonces""
                (
                    ""Id"" uuid NOT NULL DEFAULT gen_random_uuid(),
                    ""Nonce"" character varying(256) COLLATE pg_catalog.""default"" NOT NULL,
                    ""UserId"" character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    ""AdUnitId"" character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    ""Purpose"" character varying(64) COLLATE pg_catalog.""default"",
                    ""Used"" boolean NOT NULL DEFAULT false,
                    ""ExpiresAt"" timestamp with time zone NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""UsedAt"" timestamp with time zone,
                    ""Coins"" integer,
                    CONSTRAINT ""RewardNonces_pkey"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_RewardNonces_Users_UserId"" FOREIGN KEY (""UserId"")
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""RewardTransactions""
                (
                    ""Id"" uuid NOT NULL DEFAULT gen_random_uuid(),
                    ""TransactionId"" character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    ""UserId"" character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    ""Coins"" numeric(18,2) NOT NULL,
                    ""AdUnitId"" character varying(128) COLLATE pg_catalog.""default"",
                    ""RewardItem"" character varying(64) COLLATE pg_catalog.""default"",
                    ""RewardAmountRaw"" double precision,
                    ""SsvKeyId"" integer,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""RawQuery"" text COLLATE pg_catalog.""default"",
                    CONSTRAINT ""RewardTransactions_pkey"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_RewardTransactions_Users_UserId"" FOREIGN KEY (""UserId"")
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""WithdrawalData""
                (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    user_id character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    coins double precision NOT NULL,
                    executed_at timestamp with time zone NOT NULL DEFAULT now(),
                    is_paid boolean NOT NULL DEFAULT false,
                    payment_method text COLLATE pg_catalog.""default"",
                    amount numeric,
                    currency text COLLATE pg_catalog.""default"",
                    CONSTRAINT ""WithdrawalHistory_pkey"" PRIMARY KEY (id),
                    CONSTRAINT fk_withdrawalhistory_users FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""WithdrawalMethods""
                (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    type text COLLATE pg_catalog.""default"" NOT NULL,
                    label text COLLATE pg_catalog.""default"" NOT NULL,
                    data jsonb NOT NULL,
                    verified boolean NOT NULL DEFAULT false,
                    created_at timestamp with time zone NOT NULL DEFAULT now(),
                    updated_at timestamp with time zone NOT NULL DEFAULT now(),
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    CONSTRAINT ""WithdrawalMethods_pkey"" PRIMARY KEY (id),
                    CONSTRAINT ""WithdrawalMethods_type_check"" CHECK (type = ANY (ARRAY['bank'::text, 'paypal'::text, 'crypto'::text]))
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".idx_wm_type ON ""BetsTrading"".""WithdrawalMethods"" USING btree (type COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""PriceBetsUSD""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""PriceBetsUSD_id_seq""'::regclass),
                    ticker text COLLATE pg_catalog.""default"" NOT NULL,
                    price_bet numeric NOT NULL,
                    paid boolean NOT NULL,
                    margin numeric NOT NULL,
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    bet_date timestamp without time zone NOT NULL,
                    end_date timestamp without time zone NOT NULL,
                    archived boolean NOT NULL,
                    prize integer NOT NULL,
                    CONSTRAINT ""PriceBetsUSD_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");

            // ---- DailyLoginStreak (depends on Users) ----
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""DailyLoginStreak""
                (
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    last_claimed_at timestamp without time zone,
                    streak_day integer NOT NULL DEFAULT 0,
                    CONSTRAINT ""DailyLoginStreak_pkey"" PRIMARY KEY (user_id),
                    CONSTRAINT ""DailyLoginStreak_user_id_fkey"" FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");

            // ---- FinancialAssets, BetZones, Bets, etc. ----
            migrationBuilder.Sql(@"
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""FinancialAssets_id_seq"";
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""BetZones_id_seq"";
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""BetZonesUSD_id_seq"";
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""InvestmentData_id_seq"";
                CREATE SEQUENCE IF NOT EXISTS ""BetsTrading"".""PriceBets_id_seq"";
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""FinancialAssets""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""FinancialAssets_id_seq""'::regclass),
                    name text COLLATE pg_catalog.""default"" NOT NULL,
                    ""group"" text COLLATE pg_catalog.""default"" NOT NULL,
                    icon text COLLATE pg_catalog.""default"",
                    country text COLLATE pg_catalog.""default"",
                    ticker text COLLATE pg_catalog.""default"" NOT NULL,
                    current_eur numeric,
                    current_usd numeric,
                    CONSTRAINT ""FinancialAssets_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""AssetCandles""
                (
                    ""AssetId"" integer NOT NULL,
                    exchange text COLLATE pg_catalog.""default"" NOT NULL,
                    ""interval"" text COLLATE pg_catalog.""default"" NOT NULL,
                    datetime timestamp with time zone NOT NULL,
                    open numeric(20,8) NOT NULL,
                    high numeric(20,8) NOT NULL,
                    low numeric(20,8) NOT NULL,
                    close numeric(20,8) NOT NULL,
                    ""raw"" jsonb,
                    CONSTRAINT ""AssetCandles_pkey"" PRIMARY KEY (""AssetId"", exchange, ""interval"", datetime),
                    CONSTRAINT ""AssetCandles_asset_id_fkey"" FOREIGN KEY (""AssetId"")
                        REFERENCES ""BetsTrading"".""FinancialAssets"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".ix_assetcandles_asset_interval_dt ON ""BetsTrading"".""AssetCandles"" USING btree (""AssetId"" ASC NULLS LAST, ""interval"" COLLATE pg_catalog.""default"" ASC NULLS LAST, datetime DESC NULLS FIRST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".ix_assetcandles_dt_brin ON ""BetsTrading"".""AssetCandles"" USING brin (datetime) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""AssetCandlesUSD""
                (
                    ""AssetId"" integer NOT NULL,
                    exchange text COLLATE pg_catalog.""default"" NOT NULL,
                    ""interval"" text COLLATE pg_catalog.""default"" NOT NULL,
                    datetime timestamp with time zone NOT NULL,
                    open numeric(20,8) NOT NULL,
                    high numeric(20,8) NOT NULL,
                    low numeric(20,8) NOT NULL,
                    close numeric(20,8) NOT NULL,
                    ""raw"" jsonb,
                    CONSTRAINT ""AssetCandlesUSD_pkey"" PRIMARY KEY (""AssetId"", exchange, ""interval"", datetime),
                    CONSTRAINT ""AssetCandlesUSD_asset_id_fkey"" FOREIGN KEY (""AssetId"")
                        REFERENCES ""BetsTrading"".""FinancialAssets"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".ix_assetcandlesusd_asset_interval_dt ON ""BetsTrading"".""AssetCandlesUSD"" USING btree (""AssetId"" ASC NULLS LAST, ""interval"" COLLATE pg_catalog.""default"" ASC NULLS LAST, datetime DESC NULLS FIRST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".ix_assetcandlesusd_dt_brin ON ""BetsTrading"".""AssetCandlesUSD"" USING brin (datetime) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""BetZones""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""BetZones_id_seq""'::regclass),
                    ticker text COLLATE pg_catalog.""default"" NOT NULL,
                    target_value numeric NOT NULL,
                    bet_margin numeric NOT NULL,
                    start_date timestamp without time zone NOT NULL,
                    end_date timestamp without time zone,
                    target_odds numeric NOT NULL,
                    bet_type integer NOT NULL,
                    active boolean,
                    timeframe integer,
                    CONSTRAINT ""BetZones_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""BetZonesUSD""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""BetZonesUSD_id_seq""'::regclass),
                    ticker text COLLATE pg_catalog.""default"" NOT NULL,
                    target_value numeric NOT NULL,
                    bet_margin numeric NOT NULL,
                    start_date timestamp without time zone NOT NULL,
                    end_date timestamp without time zone,
                    target_odds numeric NOT NULL,
                    bet_type integer NOT NULL,
                    active boolean,
                    timeframe integer,
                    CONSTRAINT ""BetZonesUSD_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""Bets""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""InvestmentData_id_seq""'::regclass),
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    ticker character varying(255) COLLATE pg_catalog.""default"" NOT NULL,
                    bet_amount numeric(15,2) NOT NULL,
                    origin_value numeric(15,2) NOT NULL,
                    target_won boolean,
                    bet_zone integer NOT NULL,
                    finished boolean,
                    paid boolean,
                    origin_odds numeric NOT NULL,
                    archived boolean NOT NULL,
                    CONSTRAINT ""InvestmentData_pkey"" PRIMARY KEY (id),
                    CONSTRAINT ""Bet_bet_zone_fkey"" FOREIGN KEY (bet_zone)
                        REFERENCES ""BetsTrading"".""BetZones"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE NO ACTION,
                    CONSTRAINT ""InvestmentData_user_id_fkey"" FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE NO ACTION
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""Favorites""
                (
                    id text COLLATE pg_catalog.""default"" NOT NULL,
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    ticker text COLLATE pg_catalog.""default"",
                    CONSTRAINT ""Favorites_pkey"" PRIMARY KEY (id),
                    CONSTRAINT fk_user FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE NO ACTION
                )
                TABLESPACE pg_default;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""PaymentData""
                (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    user_id character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    payment_intent_id character varying(128) COLLATE pg_catalog.""default"" NOT NULL,
                    coins double precision NOT NULL,
                    executed_at timestamp with time zone NOT NULL DEFAULT now(),
                    is_paid boolean NOT NULL DEFAULT false,
                    payment_method text COLLATE pg_catalog.""default"",
                    amount numeric,
                    currency text COLLATE pg_catalog.""default"",
                    CONSTRAINT ""PaymentHistory_pkey"" PRIMARY KEY (id),
                    CONSTRAINT fk_paymenthistory_users FOREIGN KEY (user_id)
                        REFERENCES ""BetsTrading"".""Users"" (id) MATCH SIMPLE
                        ON UPDATE NO ACTION
                        ON DELETE CASCADE
                )
                TABLESPACE pg_default;
            ");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""BetsTrading"".""IX_PaymentHistory_PaymentIntentId"" ON ""BetsTrading"".""PaymentData"" USING btree (payment_intent_id COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""BetsTrading"".""IX_PaymentHistory_UserId"" ON ""BetsTrading"".""PaymentData"" USING btree (user_id COLLATE pg_catalog.""default"" ASC NULLS LAST) TABLESPACE pg_default;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BetsTrading"".""PriceBets""
                (
                    id integer NOT NULL DEFAULT nextval('""BetsTrading"".""PriceBets_id_seq""'::regclass),
                    ticker text COLLATE pg_catalog.""default"" NOT NULL,
                    price_bet numeric NOT NULL,
                    paid boolean NOT NULL,
                    margin numeric NOT NULL,
                    user_id text COLLATE pg_catalog.""default"" NOT NULL,
                    bet_date timestamp without time zone NOT NULL,
                    end_date timestamp without time zone NOT NULL,
                    archived boolean NOT NULL,
                    prize integer NOT NULL,
                    CONSTRAINT ""PriceBets_pkey"" PRIMARY KEY (id)
                )
                TABLESPACE pg_default;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""PriceBets"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""PaymentData"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""Favorites"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""Bets"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""BetZonesUSD"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""BetZones"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""AssetCandlesUSD"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""AssetCandles"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""FinancialAssets"";");
            migrationBuilder.Sql(@"
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""PriceBets_id_seq"";
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""InvestmentData_id_seq"";
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""BetZonesUSD_id_seq"";
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""BetZones_id_seq"";
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""FinancialAssets_id_seq"";
            ");

            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""DailyLoginStreak"";");

            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""PriceBetsUSD"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""WithdrawalMethods"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""WithdrawalData"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""RewardTransactions"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""RewardNonces"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""Raffles"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""RaffleItems"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""VerificationCodes"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""CopyTradingSubscriptions"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""BetsTrading"".""Users"";");
            migrationBuilder.Sql(@"
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""PriceBetsUSD_id_seq"";
                DROP SEQUENCE IF EXISTS ""BetsTrading"".""VerificationCodes_Id_seq"";
            ");
        }
    }
}
