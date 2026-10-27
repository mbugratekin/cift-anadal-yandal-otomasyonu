using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenelBelgeKaliciArsiv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GenelArsivdeMi",
                table: "Belgeler",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_GenelArsiv",
                table: "Belgeler",
                sql: "[GenelArsivdeMi] = 0 OR ([Kapsam] = 1 AND [GenelBelgeId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_GenelArsiv",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "GenelArsivdeMi",
                table: "Belgeler");
        }
    }
}
