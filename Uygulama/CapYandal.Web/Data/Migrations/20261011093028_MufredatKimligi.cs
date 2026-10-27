using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MufredatKimligi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal",
                table: "DersSablonlari");

            migrationBuilder.AddColumn<Guid>(
                name: "MufredatSablonId",
                table: "OgrenciProgramlari",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MufredatNo",
                table: "DersSablonlari",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Yalnız şablon kimliklerini numaralandırır; öğrenci ataması yapmaz.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY Tur, HedefBolum, KaynakAnaDal
                        ORDER BY OlusturulmaAni, Id) AS No
                    FROM DersSablonlari
                )
                UPDATE t SET MufredatNo = n.No
                FROM DersSablonlari t JOIN numbered n ON n.Id = t.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OgrenciProgramlari_MufredatSablonId",
                table: "OgrenciProgramlari",
                column: "MufredatSablonId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal_MufredatNo",
                table: "DersSablonlari",
                columns: new[] { "Tur", "HedefBolum", "KaynakAnaDal", "MufredatNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sablon_MufredatNo",
                table: "DersSablonlari",
                sql: "[MufredatNo] >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_OgrenciProgramlari_DersSablonlari_MufredatSablonId",
                table: "OgrenciProgramlari",
                column: "MufredatSablonId",
                principalTable: "DersSablonlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM OgrenciProgramlari WHERE MufredatSablonId IS NOT NULL)
                    THROW 51000, N'Müfredat atamaları korunmalı; otomatik geri alma yapılamaz.', 1;
                IF EXISTS (SELECT 1 FROM DersSablonlari WHERE EtkinMi = 1
                    GROUP BY Tur, HedefBolum, KaynakAnaDal HAVING COUNT(*) > 1)
                    THROW 51000, N'Birlikte kullanılan müfredatlar korunmalı; otomatik geri alma yapılamaz.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_OgrenciProgramlari_DersSablonlari_MufredatSablonId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_OgrenciProgramlari_MufredatSablonId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal_MufredatNo",
                table: "DersSablonlari");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sablon_MufredatNo",
                table: "DersSablonlari");

            migrationBuilder.DropColumn(
                name: "MufredatSablonId",
                table: "OgrenciProgramlari");

            migrationBuilder.DropColumn(
                name: "MufredatNo",
                table: "DersSablonlari");

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal",
                table: "DersSablonlari",
                columns: new[] { "Tur", "HedefBolum", "KaynakAnaDal" },
                unique: true,
                filter: "[EtkinMi] = 1");
        }
    }
}
