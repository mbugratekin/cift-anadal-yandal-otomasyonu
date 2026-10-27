using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DersKayitKapisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DersKayitKapilari",
                columns: table => new
                {
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcikMi = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AcilisAniUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    KapanisAniUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlanlayanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersKayitKapilari", x => x.DonemId);
                    table.CheckConstraint("CK_KayitKapisi_Acilis", "[AcikMi] = 0 OR [AcilisAniUtc] IS NULL");
                    table.CheckConstraint("CK_KayitKapisi_Planlayan", "([AcilisAniUtc] IS NULL AND [KapanisAniUtc] IS NULL) OR [PlanlayanKullaniciId] IS NOT NULL");
                    table.CheckConstraint("CK_KayitKapisi_Sira", "[AcilisAniUtc] IS NULL OR [KapanisAniUtc] IS NULL OR [KapanisAniUtc] > [AcilisAniUtc]");
                    table.ForeignKey(
                        name: "FK_DersKayitKapilari_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersKayitKapilari_Kullanicilar_PlanlayanKullaniciId",
                        column: x => x.PlanlayanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DersKayitKapilari_PlanlayanKullaniciId",
                table: "DersKayitKapilari",
                column: "PlanlayanKullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DersKayitKapilari");
        }
    }
}
