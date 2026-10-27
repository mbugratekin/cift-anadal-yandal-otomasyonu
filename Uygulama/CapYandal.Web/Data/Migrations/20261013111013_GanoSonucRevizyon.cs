using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GanoSonucRevizyon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GanoKontrolSonuclari_GanoKaydiId_KuralKodu",
                table: "GanoKontrolSonuclari");

            migrationBuilder.AddColumn<decimal>(
                name: "GanoKopyasi",
                table: "GanoKontrolSonuclari",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevizyonNo",
                table: "GanoKontrolSonuclari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolSonuclari_GanoKaydiId_KuralKodu_RevizyonNo",
                table: "GanoKontrolSonuclari",
                columns: new[] { "GanoKaydiId", "KuralKodu", "RevizyonNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GanoSonuc_Revizyon",
                table: "GanoKontrolSonuclari",
                sql: "([RevizyonNo] IS NULL AND [GanoKopyasi] IS NULL) OR ([RevizyonNo] IS NOT NULL AND [RevizyonNo] >= 1 AND [GanoKopyasi] IS NOT NULL AND [GanoKopyasi] BETWEEN 0 AND 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting would discard immutable snapshots and may collapse revisions.
            migrationBuilder.Sql("THROW 51000, N'GANO revizyon geri geçişi otomatik değildir; doğrulanmış yedek ve kaynaklarla ayrı geri alma planı gerekir.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_GanoKontrolSonuclari_GanoKaydiId_KuralKodu_RevizyonNo",
                table: "GanoKontrolSonuclari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GanoSonuc_Revizyon",
                table: "GanoKontrolSonuclari");

            migrationBuilder.DropColumn(
                name: "GanoKopyasi",
                table: "GanoKontrolSonuclari");

            migrationBuilder.DropColumn(
                name: "RevizyonNo",
                table: "GanoKontrolSonuclari");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolSonuclari_GanoKaydiId_KuralKodu",
                table: "GanoKontrolSonuclari",
                columns: new[] { "GanoKaydiId", "KuralKodu" },
                unique: true);
        }
    }
}
