using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DersKalemiSablonFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DersSecimKalemleri_SablonDonemDersleri_SablonId_DonemId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SablonDersi_Yariyil",
                table: "SablonDersleri");

            migrationBuilder.DropIndex(
                name: "IX_DersSecimKalemleri_SablonId_DonemId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SablonDersi_Yariyil",
                table: "SablonDersleri",
                sql: "[MufredatYariyili] BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_SablonId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "SablonId", "DersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DersSecimKalemleri_SablonDersleri_SablonId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "SablonId", "DersId" },
                principalTable: "SablonDersleri",
                principalColumns: new[] { "SablonId", "DersId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DersSecimKalemleri_SablonDersleri_SablonId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SablonDersi_Yariyil",
                table: "SablonDersleri");

            migrationBuilder.DropIndex(
                name: "IX_DersSecimKalemleri_SablonId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SablonDersi_Yariyil",
                table: "SablonDersleri",
                sql: "[MufredatYariyili] >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_SablonId_DonemId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "SablonId", "DonemId", "DersId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DersSecimKalemleri_SablonDonemDersleri_SablonId_DonemId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "SablonId", "DonemId", "DersId" },
                principalTable: "SablonDonemDersleri",
                principalColumns: new[] { "SablonId", "DonemId", "DersId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
