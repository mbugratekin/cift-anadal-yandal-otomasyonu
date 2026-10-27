using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MufredatAdi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MufredatAdi",
                table: "DersSablonlari",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal_MufredatAdi",
                table: "DersSablonlari",
                columns: new[] { "Tur", "HedefBolum", "KaynakAnaDal", "MufredatAdi" },
                unique: true,
                filter: "[MufredatAdi] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sablon_MufredatAdi_Bicim",
                table: "DersSablonlari",
                sql: "[MufredatAdi] IS NULL OR (LEN(LTRIM(RTRIM([MufredatAdi]))) > 0 AND DATALENGTH([MufredatAdi]) = DATALENGTH(LTRIM(RTRIM([MufredatAdi]))))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal_MufredatAdi",
                table: "DersSablonlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sablon_MufredatAdi_Bicim",
                table: "DersSablonlari");

            migrationBuilder.DropColumn(
                name: "MufredatAdi",
                table: "DersSablonlari");
        }
    }
}
