using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AkademikKosulSurumu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AkademikKosulSurumuId",
                table: "KosulKumeleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                table: "KosulDegerleri",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Amac",
                table: "KosulDegerleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KontrolTuru",
                table: "KosulDegerleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KuralKimligi",
                table: "KosulDegerleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiraNo",
                table: "KosulDegerleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AkademikKosulSurumleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramTuru = table.Column<int>(type: "int", nullable: false),
                    SurumNo = table.Column<int>(type: "int", nullable: false),
                    TamamlandiMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturulmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AkademikKosulSurumleri", x => x.Id);
                    table.UniqueConstraint("AK_AkademikKosulSurumleri_Id_ProgramTuru", x => new { x.Id, x.ProgramTuru });
                    table.CheckConstraint("CK_AkademikKosulSurumu_Deger", "[ProgramTuru] IN (1, 2) AND [SurumNo] >= 1");
                    table.ForeignKey(
                        name: "FK_AkademikKosulSurumleri_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KosulKumeleri_AkademikKosulSurumuId_Kapsam",
                table: "KosulKumeleri",
                columns: new[] { "AkademikKosulSurumuId", "Kapsam" },
                unique: true,
                filter: "[AkademikKosulSurumuId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KosulKumeleri_AkademikKosulSurumuId_ProgramTuru",
                table: "KosulKumeleri",
                columns: new[] { "AkademikKosulSurumuId", "ProgramTuru" });

            migrationBuilder.CreateIndex(
                name: "IX_KosulDegerleri_KosulKumesiId_KuralKimligi",
                table: "KosulDegerleri",
                columns: new[] { "KosulKumesiId", "KuralKimligi" },
                unique: true,
                filter: "[KuralKimligi] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KosulDegerleri_KosulKumesiId_SiraNo",
                table: "KosulDegerleri",
                columns: new[] { "KosulKumesiId", "SiraNo" },
                unique: true,
                filter: "[SiraNo] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri",
                sql: "[KontrolTuru] IS NULL OR ([NormalEsik] IS NOT NULL AND [ReferansDegeri] IS NULL AND [DegerlendirmeTuru] = 1 AND (([KontrolTuru] = 1 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NULL AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 2 AND [AltEsik] IS NOT NULL AND [IzinSayisi] IS NOT NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 1 AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 3 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 2 AND [ArdisikDonemSayisi] IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Kosul_Tanim",
                table: "KosulDegerleri",
                sql: "([KuralKimligi] IS NULL AND [Amac] IS NULL AND [KontrolTuru] IS NULL AND [SiraNo] IS NULL AND [Aciklama] IS NULL) OR ([KuralKimligi] IS NOT NULL AND [KuralKimligi] <> '00000000-0000-0000-0000-000000000000' AND [Amac] IS NOT NULL AND [Amac] IN (1, 2) AND [KontrolTuru] IS NOT NULL AND [KontrolTuru] IN (1, 2, 3) AND [SiraNo] IS NOT NULL AND [SiraNo] >= 1 AND [Aciklama] IS NOT NULL AND LEN(LTRIM(RTRIM([Aciklama]))) > 0 AND [KuralKodu] = REPLACE(CONVERT(nvarchar(36), [KuralKimligi]), '-', ''))");
            migrationBuilder.AddCheckConstraint(
                name: "CK_KosulKumesi_SurumKapsam",
                table: "KosulKumeleri",
                sql: "[AkademikKosulSurumuId] IS NULL OR ([ProgramTuru] = 1 AND [Kapsam] IN (1, 2)) OR ([ProgramTuru] = 2 AND [Kapsam] IN (1, 3))");

            migrationBuilder.CreateIndex(
                name: "IX_AkademikKosulSurumleri_OlusturanKullaniciId",
                table: "AkademikKosulSurumleri",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_AkademikKosulSurumleri_ProgramTuru_SurumNo",
                table: "AkademikKosulSurumleri",
                columns: new[] { "ProgramTuru", "SurumNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KosulKumeleri_AkademikKosulSurumleri_AkademikKosulSurumuId_ProgramTuru",
                table: "KosulKumeleri",
                columns: new[] { "AkademikKosulSurumuId", "ProgramTuru" },
                principalTable: "AkademikKosulSurumleri",
                principalColumns: new[] { "Id", "ProgramTuru" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [dbo].[TR_AkademikKosulSurumleri_Degismez]
                ON [dbo].[AkademikKosulSurumleri] AFTER UPDATE, DELETE AS
                BEGIN
                  SET NOCOUNT ON; SET XACT_ABORT OFF;
                  IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
                    WHERE i.Id IS NULL OR d.TamamlandiMi=1 OR i.TamamlandiMi=0
                    OR d.ProgramTuru<>i.ProgramTuru OR d.SurumNo<>i.SurumNo
                    OR d.OlusturanKullaniciId<>i.OlusturanKullaniciId OR d.OlusturulmaAni<>i.OlusturulmaAni)
                    THROW 51001,N''Koşul sürümü değişmez; yalnız ilk tamamlanma geçişi izinlidir.'',1;
                END');
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [dbo].[TR_KosulKumeleri_SurumKoruma]
                ON [dbo].[KosulKumeleri] AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                  SET NOCOUNT ON; SET XACT_ABORT OFF;
                  IF EXISTS (SELECT 1 FROM deleted d WHERE d.AkademikKosulSurumuId IS NOT NULL
                    OR EXISTS(SELECT 1 FROM dbo.GanoKontrolSonuclari r WHERE r.KosulKumesiId=d.Id))
                    THROW 51001,N''Kullanılmış veya sürümlü koşul kümesi değişmez.'',1;
                  IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.AkademikKosulSurumleri v ON v.Id=i.AkademikKosulSurumuId
                    WHERE v.TamamlandiMi=1 OR EXISTS(SELECT 1 FROM deleted d WHERE d.Id=i.Id))
                    THROW 51001,N''Tamamlanmış sürüme koşul eklenemez veya eski küme atanamaz.'',1;
                END');
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [dbo].[TR_KosulDegerleri_SurumKoruma]
                ON [dbo].[KosulDegerleri] AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                  SET NOCOUNT ON; SET XACT_ABORT OFF;
                  IF EXISTS (SELECT 1 FROM deleted d JOIN dbo.KosulKumeleri k ON k.Id=d.KosulKumesiId
                    WHERE k.AkademikKosulSurumuId IS NOT NULL
                    OR EXISTS(SELECT 1 FROM dbo.GanoKontrolSonuclari r WHERE r.KosulKumesiId=d.KosulKumesiId AND r.KuralKodu=d.KuralKodu))
                    THROW 51001,N''Kullanılmış veya sürümlü kural değişmez.'',1;
                  IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.KosulKumeleri k ON k.Id=i.KosulKumesiId
                    LEFT JOIN dbo.AkademikKosulSurumleri v ON v.Id=k.AkademikKosulSurumuId
                    WHERE v.TamamlandiMi=1 OR (k.AkademikKosulSurumuId IS NULL AND i.KuralKimligi IS NOT NULL)
                    OR (k.AkademikKosulSurumuId IS NOT NULL AND i.KuralKimligi IS NULL)
                    OR EXISTS(SELECT 1 FROM dbo.GanoKontrolSonuclari r WHERE r.KosulKumesiId=k.Id))
                    THROW 51001,N''Tamamlanmış sürüme kural eklenemez; eski kuralın tanımı tahminle doldurulamaz.'',1;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("THROW 51000, N'Akademik koşul sürümlerinin otomatik geri geçişi veri kaybıdır; ayrı yedek/kaynak planı gerekir.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_KosulKumeleri_AkademikKosulSurumleri_AkademikKosulSurumuId_ProgramTuru",
                table: "KosulKumeleri");

            migrationBuilder.DropTable(
                name: "AkademikKosulSurumleri");

            migrationBuilder.DropIndex(
                name: "IX_KosulKumeleri_AkademikKosulSurumuId_Kapsam",
                table: "KosulKumeleri");

            migrationBuilder.DropIndex(
                name: "IX_KosulKumeleri_AkademikKosulSurumuId_ProgramTuru",
                table: "KosulKumeleri");

            migrationBuilder.DropIndex(
                name: "IX_KosulDegerleri_KosulKumesiId_KuralKimligi",
                table: "KosulDegerleri");

            migrationBuilder.DropIndex(
                name: "IX_KosulDegerleri_KosulKumesiId_SiraNo",
                table: "KosulDegerleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Kosul_Tanim",
                table: "KosulDegerleri");

            migrationBuilder.DropColumn(
                name: "AkademikKosulSurumuId",
                table: "KosulKumeleri");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                table: "KosulDegerleri");

            migrationBuilder.DropColumn(
                name: "Amac",
                table: "KosulDegerleri");

            migrationBuilder.DropColumn(
                name: "KontrolTuru",
                table: "KosulDegerleri");

            migrationBuilder.DropColumn(
                name: "KuralKimligi",
                table: "KosulDegerleri");

            migrationBuilder.DropColumn(
                name: "SiraNo",
                table: "KosulDegerleri");
        }
    }
}
