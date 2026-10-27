using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DersKalemiSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KrediKopyasi",
                table: "DersSecimKalemleri",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MufredatYariyiliKopyasi",
                table: "DersSecimKalemleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurKopyasi",
                table: "DersSecimKalemleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersKalemi_Kredi",
                table: "DersSecimKalemleri",
                sql: "[KrediKopyasi] IS NULL OR [KrediKopyasi] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersKalemi_Tur",
                table: "DersSecimKalemleri",
                sql: "[TurKopyasi] IS NULL OR [TurKopyasi] IN (1,2,3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersKalemi_Yariyil",
                table: "DersSecimKalemleri",
                sql: "[MufredatYariyiliKopyasi] IS NULL OR [MufredatYariyiliKopyasi] BETWEEN 1 AND 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DersKalemi_Kredi",
                table: "DersSecimKalemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DersKalemi_Tur",
                table: "DersSecimKalemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DersKalemi_Yariyil",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "KrediKopyasi",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "MufredatYariyiliKopyasi",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "TurKopyasi",
                table: "DersSecimKalemleri");
        }
    }
}
