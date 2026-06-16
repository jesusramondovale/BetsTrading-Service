using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetsTrading.Infrastructure.Migrations;

/// <summary>
/// Impide suscripciones de copy-trading donde follower y target son el mismo usuario.
/// </summary>
[Migration("20260616120000_PreventSelfCopyTrading")]
public partial class PreventSelfCopyTrading : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE ""BetsTrading"".""CopyTradingSubscriptions""
            SET is_active = false,
                stopped_at = COALESCE(stopped_at, now()),
                stop_reason = COALESCE(stop_reason, 'self_copy_not_allowed'),
                updated_at = now()
            WHERE lower(follower_user_id) = lower(target_user_id)
              AND is_active = true;
        ");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'CopyTradingSubscriptions_no_self_copy'
                ) THEN
                    ALTER TABLE ""BetsTrading"".""CopyTradingSubscriptions""
                    ADD CONSTRAINT ""CopyTradingSubscriptions_no_self_copy""
                    CHECK (lower(follower_user_id) <> lower(target_user_id));
                END IF;
            END $$;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""BetsTrading"".""CopyTradingSubscriptions""
            DROP CONSTRAINT IF EXISTS ""CopyTradingSubscriptions_no_self_copy"";
        ");
    }
}
