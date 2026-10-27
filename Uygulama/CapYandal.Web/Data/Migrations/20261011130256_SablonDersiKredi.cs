using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SablonDersiKredi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Kredi",
                table: "SablonDersleri",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SablonDersi_Kredi",
                table: "SablonDersleri",
                sql: "[Kredi] IS NULL OR [Kredi] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SablonDersi_Kredi",
                table: "SablonDersleri");

            migrationBuilder.DropColumn(
                name: "Kredi",
                table: "SablonDersleri");
        }
    }
}
