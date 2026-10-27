using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonelGorusmeBilgisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonelGorusmeBilgileri",
                columns: table => new
                {
                    KullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Blok = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Oda = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AraliklarJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DegisiklikAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonelGorusmeBilgileri", x => x.KullaniciId);
                    table.CheckConstraint("CK_Gorusme_Json", "ISJSON([AraliklarJson]) = 1");
                    table.ForeignKey(
                        name: "FK_PersonelGorusmeBilgileri_Kullanicilar_KullaniciId",
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
                name: "PersonelGorusmeBilgileri");
        }
    }
}
