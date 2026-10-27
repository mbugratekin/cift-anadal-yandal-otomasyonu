using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DersBazindaDuzeltmeTalebi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_DersSecimKalemleri_Id_ListeId",
                table: "DersSecimKalemleri",
                columns: new[] { "Id", "ListeId" });

            migrationBuilder.CreateTable(
                name: "DersDuzeltmeTalepleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KalemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsteyenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IstekAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Gerekce = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    KapatanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KapatilmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersDuzeltmeTalepleri", x => x.Id);
                    table.CheckConstraint("CK_DersDuzeltme_Gerekce", "LEN(LTRIM(RTRIM([Gerekce])))>0");
                    table.CheckConstraint("CK_DersDuzeltme_Kapanis", "([KapatanKullaniciId] IS NULL AND [KapatilmaAni] IS NULL) OR ([KapatanKullaniciId] IS NOT NULL AND [KapatilmaAni] IS NOT NULL AND [KapatilmaAni]>=[IstekAni])");
                    table.ForeignKey(
                        name: "FK_DersDuzeltmeTalepleri_DersSecimKalemleri_KalemId_ListeId",
                        columns: x => new { x.KalemId, x.ListeId },
                        principalTable: "DersSecimKalemleri",
                        principalColumns: new[] { "Id", "ListeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersDuzeltmeTalepleri_Kullanicilar_IsteyenKullaniciId",
                        column: x => x.IsteyenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersDuzeltmeTalepleri_Kullanicilar_KapatanKullaniciId",
                        column: x => x.KapatanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DersDuzeltmeTalepleri_IsteyenKullaniciId",
                table: "DersDuzeltmeTalepleri",
                column: "IsteyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersDuzeltmeTalepleri_KalemId_ListeId",
                table: "DersDuzeltmeTalepleri",
                columns: new[] { "KalemId", "ListeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DersDuzeltmeTalepleri_KapatanKullaniciId",
                table: "DersDuzeltmeTalepleri",
                column: "KapatanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersDuzeltmeTalepleri_ListeId_KalemId",
                table: "DersDuzeltmeTalepleri",
                columns: new[] { "ListeId", "KalemId" },
                unique: true,
                filter: "[KapatilmaAni] IS NULL");
            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_DersDuzeltmeTalepleri_Koru] ON [DersDuzeltmeTalepleri]
                AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                        THROW 51000, N'Ders düzeltme isteği silinemez.', 1;
                    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE
                        d.KapatilmaAni IS NOT NULL OR i.ListeId<>d.ListeId OR i.KalemId<>d.KalemId OR
                        i.IsteyenKullaniciId<>d.IsteyenKullaniciId OR i.IstekAni<>d.IstekAni OR
                        i.Gerekce COLLATE Latin1_General_100_BIN2 <> d.Gerekce COLLATE Latin1_General_100_BIN2)
                        THROW 51000, N'Ders düzeltme isteğinin geçmişi değiştirilemez.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DersDuzeltmeTalepleri");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DersSecimKalemleri_Id_ListeId",
                table: "DersSecimKalemleri");
        }
    }
}
