using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class KaynakAnaDalVeMazeretliKayit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MazeretliMi",
                table: "DersSecimleri",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "KaynakAnaDallar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KaynakAnaDallar", x => x.Id);
                    table.CheckConstraint("CK_KaynakAnaDal_Tur", "[Tur] IN (1,2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_KaynakAnaDallar_Tur_Ad",
                table: "KaynakAnaDallar",
                columns: new[] { "Tur", "Ad" },
                unique: true);
            // Başlangıç seçenekleri kullanıcı kararıdır; var olan öğrenciler taşınmaz.
            migrationBuilder.Sql(@"INSERT INTO KaynakAnaDallar (Id,Tur,Ad,EtkinMi)
SELECT NEWID(),v.Tur,v.Ad,1 FROM (VALUES
(1,N'Elektrik-Elektronik Mühendisliği'),(1,N'Makine Mühendisliği'),
(2,N'Elektrik-Elektronik Mühendisliği'),(2,N'Matematik'),(2,N'Fizik'),(2,N'İstatistik')) v(Tur,Ad);
INSERT INTO KaynakAnaDallar (Id,Tur,Ad,EtkinMi)
SELECT NEWID(),t.Tur,t.KaynakAnaDal,1 FROM DersSablonlari t
WHERE t.KaynakAnaDal IS NOT NULL AND NOT EXISTS
(SELECT 1 FROM KaynakAnaDallar d WHERE d.Tur=t.Tur AND d.Ad=t.KaynakAnaDal)
GROUP BY t.Tur,t.KaynakAnaDal;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KaynakAnaDallar");

            migrationBuilder.DropColumn(
                name: "MazeretliMi",
                table: "DersSecimleri");
        }
    }
}
