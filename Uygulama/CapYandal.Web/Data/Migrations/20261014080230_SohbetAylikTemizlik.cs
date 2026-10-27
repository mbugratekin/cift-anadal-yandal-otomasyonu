using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SohbetAylikTemizlik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SabitlendiMi",
                table: "SohbetMesajlari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SohbetTemizlikDurumlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SonIslenenDonem = table.Column<int>(type: "int", nullable: false),
                    SonCalismaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SohbetTemizlikDurumlari", x => x.Id);
                    table.CheckConstraint("CK_SohbetTemizlik_Tek", "[Id] = 1");
                });
            // İlk temizlik bir sonraki ayın başında çalışır.
            migrationBuilder.Sql("INSERT INTO [SohbetTemizlikDurumlari] ([Id],[SonIslenenDonem],[SonCalismaAni]) SELECT 1,YEAR(SYSDATETIMEOFFSET() AT TIME ZONE 'Turkey Standard Time')*100+MONTH(SYSDATETIMEOFFSET() AT TIME ZONE 'Turkey Standard Time'),NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SohbetTemizlikDurumlari");

            migrationBuilder.DropColumn(
                name: "SabitlendiMi",
                table: "SohbetMesajlari");
        }
    }
}
