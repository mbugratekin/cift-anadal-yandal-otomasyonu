using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramTakipNotlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramTakipNotlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metin = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DuzeltenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuzenlemeAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramTakipNotlari", x => x.Id);
                    table.CheckConstraint("CK_ProgramTakipNotu_Metin", "LEN(LTRIM(RTRIM([Metin]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProgramTakipNotlari_Kullanicilar_DuzeltenKullaniciId",
                        column: x => x.DuzeltenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramTakipNotlari_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramTakipNotlari_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramTakipNotlari_DuzeltenKullaniciId",
                table: "ProgramTakipNotlari",
                column: "DuzeltenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramTakipNotlari_OlusturanKullaniciId",
                table: "ProgramTakipNotlari",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramTakipNotlari_ProgramId",
                table: "ProgramTakipNotlari",
                column: "ProgramId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramTakipNotlari");
        }
    }
}
