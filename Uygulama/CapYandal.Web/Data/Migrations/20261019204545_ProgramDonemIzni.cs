using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProgramDonemIzni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramDonemIzinleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Baslangic = table.Column<int>(type: "int", nullable: false),
                    Bitis = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Gerekce = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BelgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KaydedenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KayitAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IptalGerekcesi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IptalEdenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IptalAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramDonemIzinleri", x => x.Id);
                    table.CheckConstraint("CK_DonemIzni_Aralik", "[Baslangic] BETWEEN 2 AND 19997 AND [Bitis] BETWEEN [Baslangic] AND 19997");
                    table.CheckConstraint("CK_DonemIzni_Iptal", "([IptalAni] IS NULL AND [IptalEdenId] IS NULL AND [IptalGerekcesi] IS NULL) OR ([IptalAni] IS NOT NULL AND [IptalEdenId] IS NOT NULL AND LEN(LTRIM(RTRIM([IptalGerekcesi]))) > 0 AND [IptalGerekcesi] IS NOT NULL)");
                    table.CheckConstraint("CK_DonemIzni_Tur", "[Tur] IN (N'Izin', N'Dondurma') AND LEN(LTRIM(RTRIM([Gerekce]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProgramDonemIzinleri_Belgeler_BelgeId",
                        column: x => x.BelgeId,
                        principalTable: "Belgeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramDonemIzinleri_Kullanicilar_IptalEdenId",
                        column: x => x.IptalEdenId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramDonemIzinleri_Kullanicilar_KaydedenId",
                        column: x => x.KaydedenId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramDonemIzinleri_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramDonemIzinleri_BelgeId",
                table: "ProgramDonemIzinleri",
                column: "BelgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramDonemIzinleri_IptalEdenId",
                table: "ProgramDonemIzinleri",
                column: "IptalEdenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramDonemIzinleri_KaydedenId",
                table: "ProgramDonemIzinleri",
                column: "KaydedenId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramDonemIzinleri_ProgramId_Baslangic_Bitis",
                table: "ProgramDonemIzinleri",
                columns: new[] { "ProgramId", "Baslangic", "Bitis" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramDonemIzinleri");
        }
    }
}
