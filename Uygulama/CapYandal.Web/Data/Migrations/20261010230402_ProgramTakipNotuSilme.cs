using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramTakipNotuSilme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SilenKullaniciId",
                table: "ProgramTakipNotlari",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SilindiMi",
                table: "ProgramTakipNotlari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SilmeAni",
                table: "ProgramTakipNotlari",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramTakipNotlari_SilenKullaniciId",
                table: "ProgramTakipNotlari",
                column: "SilenKullaniciId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramTakipNotu_Silme",
                table: "ProgramTakipNotlari",
                sql: "([SilindiMi] = 0 AND [SilmeAni] IS NULL AND [SilenKullaniciId] IS NULL) OR ([SilindiMi] = 1 AND [SilmeAni] IS NOT NULL AND [SilenKullaniciId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramTakipNotlari_Kullanicilar_SilenKullaniciId",
                table: "ProgramTakipNotlari",
                column: "SilenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramTakipNotlari_Kullanicilar_SilenKullaniciId",
                table: "ProgramTakipNotlari");

            migrationBuilder.DropIndex(
                name: "IX_ProgramTakipNotlari_SilenKullaniciId",
                table: "ProgramTakipNotlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramTakipNotu_Silme",
                table: "ProgramTakipNotlari");

            migrationBuilder.DropColumn(
                name: "SilenKullaniciId",
                table: "ProgramTakipNotlari");

            migrationBuilder.DropColumn(
                name: "SilindiMi",
                table: "ProgramTakipNotlari");

            migrationBuilder.DropColumn(
                name: "SilmeAni",
                table: "ProgramTakipNotlari");
        }
    }
}
