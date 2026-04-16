using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BetsTrading.Infrastructure.Migrations;

/// <summary>
/// Añade columna no_ads a Users (compra Stripe "sin anuncios").
/// </summary>
[Migration("20260416120000_AddUserNoAds")]
public partial class AddUserNoAds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""BetsTrading"".""Users""
            ADD COLUMN IF NOT EXISTS no_ads boolean NOT NULL DEFAULT false;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""BetsTrading"".""Users"" DROP COLUMN IF EXISTS no_ads;");
    }
}
