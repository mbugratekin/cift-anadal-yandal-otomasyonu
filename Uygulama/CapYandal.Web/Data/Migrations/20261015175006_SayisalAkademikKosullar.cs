using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SayisalAkademikKosullar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SayisalKurallarJson",
                table: "AkademikKosulSurumleri",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AkademikKosulSurumu_SayisalJson",
                table: "AkademikKosulSurumleri",
                sql: "ISJSON([SayisalKurallarJson]) = 1 AND LEFT(LTRIM([SayisalKurallarJson]), 1) = N'['");
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_AkademikKosulSurumleri_Degismez]
                ON [dbo].[AkademikKosulSurumleri] AFTER UPDATE, DELETE AS
                BEGIN
                  SET NOCOUNT ON; SET XACT_ABORT OFF;
                  IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
                    WHERE i.Id IS NULL OR d.TamamlandiMi=1 OR i.TamamlandiMi=0
                    OR d.ProgramTuru<>i.ProgramTuru OR d.SurumNo<>i.SurumNo
                    OR d.OlusturanKullaniciId<>i.OlusturanKullaniciId OR d.OlusturulmaAni<>i.OlusturulmaAni
                    OR d.SayisalKurallarJson<>i.SayisalKurallarJson)
                    THROW 51001,N'Koşul sürümü değişmez; yalnız ilk tamamlanma geçişi izinlidir.',1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.AkademikKosulSurumleri WHERE SayisalKurallarJson<>N'[]')
                  THROW 51001,N'Sayısal kural geçmişi var; veri kaybettiren geri geçiş yapılmaz.',1;
                """);
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_AkademikKosulSurumleri_Degismez]
                ON [dbo].[AkademikKosulSurumleri] AFTER UPDATE, DELETE AS
                BEGIN
                  SET NOCOUNT ON; SET XACT_ABORT OFF;
                  IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id
                    WHERE i.Id IS NULL OR d.TamamlandiMi=1 OR i.TamamlandiMi=0
                    OR d.ProgramTuru<>i.ProgramTuru OR d.SurumNo<>i.SurumNo
                    OR d.OlusturanKullaniciId<>i.OlusturanKullaniciId OR d.OlusturulmaAni<>i.OlusturulmaAni)
                    THROW 51001,N'Koşul sürümü değişmez; yalnız ilk tamamlanma geçişi izinlidir.',1;
                END
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_AkademikKosulSurumu_SayisalJson",
                table: "AkademikKosulSurumleri");

            migrationBuilder.DropColumn(
                name: "SayisalKurallarJson",
                table: "AkademikKosulSurumleri");
        }
    }
}
