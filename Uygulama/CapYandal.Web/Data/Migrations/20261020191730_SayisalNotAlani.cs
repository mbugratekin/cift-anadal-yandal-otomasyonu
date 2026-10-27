using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SayisalNotAlani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SayisalNot",
                table: "DersDenemeSurumleri",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersDenemeSurumu_SayisalNot",
                table: "DersDenemeSurumleri",
                sql: "[SayisalNot] IS NULL OR [SayisalNot] BETWEEN 0 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DersDenemeSurumu_SayisalNot",
                table: "DersDenemeSurumleri");

            migrationBuilder.DropColumn(
                name: "SayisalNot",
                table: "DersDenemeSurumleri");
        }
    }
}
