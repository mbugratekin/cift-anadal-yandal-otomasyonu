using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfilFotografi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KullaniciFotograflari",
                columns: table => new
                {
                    KullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JpegIcerik = table.Column<byte[]>(type: "varbinary(max)", maxLength: 1048576, nullable: false),
                    OnayAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KullaniciFotograflari", x => x.KullaniciId);
                    table.CheckConstraint("CK_Fotograf_Boyut", "DATALENGTH([JpegIcerik]) BETWEEN 1 AND 1048576");
                    table.ForeignKey(
                        name: "FK_KullaniciFotograflari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KullaniciFotograflari");
        }
    }
}
