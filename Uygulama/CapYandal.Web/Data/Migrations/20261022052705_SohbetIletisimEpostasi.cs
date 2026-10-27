using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SohbetIletisimEpostasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IletisimEpostasi",
                table: "PersonelGorusmeBilgileri",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IletisimEpostasi",
                table: "PersonelGorusmeBilgileri");
        }
    }
}
