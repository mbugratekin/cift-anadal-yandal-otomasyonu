using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class EsnekAltSinirVeIzinKoruma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_ProgramDonemIzinleri_Koru ON ProgramDonemIzinleri AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                        THROW 51031,N'İzin geçmişi fiziksel silinemez.',1;
                    IF EXISTS (SELECT 1 FROM deleted WHERE IptalAni IS NOT NULL)
                        THROW 51032,N'İptal edilmiş izin geçmişi değiştirilemez.',1;
                    IF UPDATE(ProgramId) OR UPDATE(Baslangic) OR UPDATE(Bitis) OR UPDATE(Tur) OR UPDATE(Gerekce)
                        OR UPDATE(BelgeId) OR UPDATE(KaydedenId) OR UPDATE(KayitAni)
                        THROW 51033,N'İzin aralığı ve dayanağı korunur; gerekçeli iptal ve yeni kayıt kullanın.',1;
                END
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri",
                sql: "[KontrolTuru] IS NULL OR ([NormalEsik] IS NOT NULL AND [ReferansDegeri] IS NULL AND [DegerlendirmeTuru] = 1 AND (([KontrolTuru] = 1 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NULL AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 2 AND [IzinSayisi] IS NOT NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 1 AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 3 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 2 AND [ArdisikDonemSayisi] IS NOT NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProgramDonemIzinleri_Koru");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Kosul_KontrolTuru",
                table: "KosulDegerleri",
                sql: "[KontrolTuru] IS NULL OR ([NormalEsik] IS NOT NULL AND [ReferansDegeri] IS NULL AND [DegerlendirmeTuru] = 1 AND (([KontrolTuru] = 1 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NULL AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 2 AND [AltEsik] IS NOT NULL AND [IzinSayisi] IS NOT NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 1 AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 3 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 2 AND [ArdisikDonemSayisi] IS NOT NULL)))");
        }
    }
}
