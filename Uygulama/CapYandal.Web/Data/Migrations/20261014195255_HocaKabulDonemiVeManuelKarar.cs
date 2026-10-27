using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HocaKabulDonemiVeManuelKarar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KabulAkademikYilBaslangici",
                table: "OgrenciProgramlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KabulYariyili",
                table: "OgrenciProgramlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "KararAni",
                table: "OgrenciProgramlari",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KararGerekcesi",
                table: "OgrenciProgramlari",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KararVerenKullaniciId",
                table: "OgrenciProgramlari",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OgrenciProgramlari",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SonKarar",
                table: "OgrenciProgramlari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OgrenciProgramlari_KararVerenKullaniciId",
                table: "OgrenciProgramlari",
                column: "KararVerenKullaniciId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Program_KabulDonemi",
                table: "OgrenciProgramlari",
                sql: "([KabulAkademikYilBaslangici] IS NULL AND [KabulYariyili] IS NULL) OR ([KabulAkademikYilBaslangici] IS NOT NULL AND [KabulYariyili] IS NOT NULL AND [KabulAkademikYilBaslangici] BETWEEN 1 AND 9998 AND [KabulYariyili] IN (1,2))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Program_Karar",
                table: "OgrenciProgramlari",
                sql: "([SonKarar] IS NULL AND [KararGerekcesi] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararAni] IS NULL) OR ([SonKarar] IS NOT NULL AND [SonKarar] IN (1,2,3) AND [AktifMi] = 0 AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0 AND [KararVerenKullaniciId] IS NOT NULL AND [KararAni] IS NOT NULL AND (([SonKarar] = 1 AND [Tur] = 1) OR ([SonKarar] = 2 AND [Tur] = 2) OR [SonKarar] = 3))");

            migrationBuilder.AddForeignKey(
                name: "FK_OgrenciProgramlari_Kullanicilar_KararVerenKullaniciId",
                table: "OgrenciProgramlari",
                column: "KararVerenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [dbo].[TR_DegisiklikGecmisi_Degismez]
                ON [dbo].[DegisiklikGecmisi] AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51001, N''İşlem geçmişi değiştirilemez veya silinemez.'', 1;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_DegisiklikGecmisi_Degismez];");
            migrationBuilder.DropForeignKey(
                name: "FK_OgrenciProgramlari_Kullanicilar_KararVerenKullaniciId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_OgrenciProgramlari_KararVerenKullaniciId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Program_KabulDonemi",
                table: "OgrenciProgramlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Program_Karar",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "KabulAkademikYilBaslangici",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "KabulYariyili",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "KararAni",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "KararGerekcesi",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "KararVerenKullaniciId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "SonKarar",
                table: "OgrenciProgramlari");
        }
    }
}
