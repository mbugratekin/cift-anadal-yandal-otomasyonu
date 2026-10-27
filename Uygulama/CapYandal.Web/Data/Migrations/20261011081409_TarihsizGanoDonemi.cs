using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TarihsizGanoDonemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Donem_Tarih",
                table: "Donemler");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BitisTarihi",
                table: "Donemler",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BaslangicTarihi",
                table: "Donemler",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donem_Tarih",
                table: "Donemler",
                sql: "([BaslangicTarihi] IS NULL AND [BitisTarihi] IS NULL) OR ([BaslangicTarihi] IS NOT NULL AND [BitisTarihi] IS NOT NULL AND [BaslangicTarihi] <= [BitisTarihi])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tarihsiz kayıt varsa sahte tarih üreterek geri dönüş yapmayız.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Donemler] WHERE [BaslangicTarihi] IS NULL OR [BitisTarihi] IS NULL) THROW 50000, 'Tarihsiz donemler korunmalidir; geri donus icin once inceleme gerekir.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Donem_Tarih",
                table: "Donemler");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BitisTarihi",
                table: "Donemler",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BaslangicTarihi",
                table: "Donemler",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Donem_Tarih",
                table: "Donemler",
                sql: "[BaslangicTarihi] <= [BitisTarihi]");
        }
    }
}
