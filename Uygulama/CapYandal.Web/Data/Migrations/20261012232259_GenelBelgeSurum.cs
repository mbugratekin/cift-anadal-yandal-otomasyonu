using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GenelBelgeSurum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GenelBelgeId",
                table: "Belgeler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "Belgeler",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SurumNo",
                table: "Belgeler",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GenelBelgeTanimlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenelBelgeTanimlari", x => x.Id);
                    table.UniqueConstraint("AK_GenelBelgeTanimlari_Id_Tur", x => new { x.Id, x.Tur });
                    table.CheckConstraint("CK_GenelBelge_Kategori", "[Tur] IN (N'Akademik takvim', N'Protokoller', N'Ders kayıt rehberi', N'Kurallar ve yönergeler', N'Ders kayıt formları')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_GenelBelgeId_SurumNo",
                table: "Belgeler",
                columns: new[] { "GenelBelgeId", "SurumNo" },
                unique: true,
                filter: "[Kapsam] = 1 AND [GenelBelgeId] IS NOT NULL AND [SurumNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_GenelBelgeId_Tur",
                table: "Belgeler",
                columns: new[] { "GenelBelgeId", "Tur" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_GenelDosyaBoyut",
                table: "Belgeler",
                sql: "[GenelBelgeId] IS NULL OR [ResmiBaglanti] IS NOT NULL OR ([BoyutBayt] IS NOT NULL AND [BoyutBayt] > 0 AND [BoyutBayt] <= 10485760)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_GenelDosyaOzet",
                table: "Belgeler",
                sql: "[GenelBelgeId] IS NULL OR [ResmiBaglanti] IS NOT NULL OR ([Sha256] IS NOT NULL AND DATALENGTH([Sha256]) = 128 AND [Sha256] COLLATE Latin1_General_BIN2 NOT LIKE N'%[^0-9A-F]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_GenelSurum",
                table: "Belgeler",
                sql: "([Kapsam] = 1 AND (([GenelBelgeId] IS NULL AND [SurumNo] IS NULL) OR ([GenelBelgeId] IS NOT NULL AND [SurumNo] IS NOT NULL AND [SurumNo] > 0))) OR ([Kapsam] = 2 AND [GenelBelgeId] IS NULL AND [SurumNo] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_GenelBelgeTanimlari_GenelBelgeId_Tur",
                table: "Belgeler",
                columns: new[] { "GenelBelgeId", "Tur" },
                principalTable: "GenelBelgeTanimlari",
                principalColumns: new[] { "Id", "Tur" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_GenelBelgeTanimlari_GenelBelgeId_Tur",
                table: "Belgeler");

            migrationBuilder.DropTable(
                name: "GenelBelgeTanimlari");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_GenelBelgeId_SurumNo",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_GenelBelgeId_Tur",
                table: "Belgeler");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_GenelDosyaBoyut",
                table: "Belgeler");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_GenelDosyaOzet",
                table: "Belgeler");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_GenelSurum",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "GenelBelgeId",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "SurumNo",
                table: "Belgeler");
        }
    }
}
