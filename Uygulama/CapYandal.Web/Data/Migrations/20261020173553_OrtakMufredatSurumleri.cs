using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrtakMufredatSurumleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MufredatId",
                table: "DersSablonlari",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mufredatlar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaslangicYili = table.Column<int>(type: "int", nullable: true),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mufredatlar", x => x.Id);
                    table.CheckConstraint("CK_Mufredat_Baslangic", "[BaslangicYili] IS NULL OR [BaslangicYili] BETWEEN 2 AND 9998");
                    table.ForeignKey(
                        name: "FK_Mufredatlar_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_MufredatId",
                table: "DersSablonlari",
                column: "MufredatId");

            migrationBuilder.CreateIndex(
                name: "IX_Mufredatlar_Ad",
                table: "Mufredatlar",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mufredatlar_BaslangicYili",
                table: "Mufredatlar",
                column: "BaslangicYili",
                unique: true,
                filter: "[BaslangicYili] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Mufredatlar_OlusturanKullaniciId",
                table: "Mufredatlar",
                column: "OlusturanKullaniciId");

            migrationBuilder.AddForeignKey(
                name: "FK_DersSablonlari_Mufredatlar_MufredatId",
                table: "DersSablonlari",
                column: "MufredatId",
                principalTable: "Mufredatlar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Yalnız kullanıcı tarafından doğrulanmış eski/yeni adları eşleştir.
            // Liste kimlikleri, dersler ve öğrenci atamaları değişmez.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM DersSablonlari WHERE HedefBolum=N'Bilgisayar Mühendisliği'
                    AND ISNULL(MufredatAdi,N'') NOT IN (N'Yeni Müfredat (2023 ve sonrası)',N'Eski Müfredat (2023 öncesi)',N'Bilgisayar Mühendisliği Yan Dal Müfredatı'))
                    THROW 51000, N'Bilinmeyen müfredat adı: geçişten önce açık eşleştirme gerekli.', 1;
                DECLARE @Eski uniqueidentifier=NEWID(), @Yeni uniqueidentifier=NEWID();
                IF EXISTS (SELECT 1 FROM DersSablonlari WHERE HedefBolum=N'Bilgisayar Mühendisliği')
                BEGIN
                    DECLARE @Actor uniqueidentifier=(SELECT TOP(1) OlusturanKullaniciId FROM DersSablonlari WHERE HedefBolum=N'Bilgisayar Mühendisliği' ORDER BY Id);
                    INSERT INTO Mufredatlar(Id,Ad,BaslangicYili,EtkinMi,OlusturanKullaniciId,OlusturmaAni)
                    VALUES (@Eski,N'Eski Müfredat',NULL,1,@Actor,SYSDATETIMEOFFSET()),(@Yeni,N'Yeni Müfredat',2023,1,@Actor,SYSDATETIMEOFFSET());
                    UPDATE DersSablonlari SET MufredatId=CASE WHEN MufredatAdi=N'Eski Müfredat (2023 öncesi)' THEN @Eski ELSE @Yeni END
                    WHERE HedefBolum=N'Bilgisayar Mühendisliği';
                    IF NOT EXISTS (SELECT 1 FROM DersSablonlari WHERE MufredatId=@Eski AND Tur=2)
                        INSERT INTO DersSablonlari(Id,MufredatId,Tur,HedefBolum,KaynakAnaDal,Ad,MufredatNo,MufredatAdi,EtkinMi,OlusturanKullaniciId,OlusturulmaAni)
                        SELECT NEWID(),@Eski,2,N'Bilgisayar Mühendisliği',NULL,N'Bilgisayar Mühendisliği',
                            ISNULL(MAX(MufredatNo),0)+1,N'Eski Müfredat',1,@Actor,SYSDATETIMEOFFSET()
                        FROM DersSablonlari WHERE Tur=2 AND HedefBolum=N'Bilgisayar Mühendisliği';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DersSablonlari_Mufredatlar_MufredatId",
                table: "DersSablonlari");

            migrationBuilder.DropTable(
                name: "Mufredatlar");

            migrationBuilder.DropIndex(
                name: "IX_DersSablonlari_MufredatId",
                table: "DersSablonlari");

            migrationBuilder.DropColumn(
                name: "MufredatId",
                table: "DersSablonlari");
        }
    }
}
