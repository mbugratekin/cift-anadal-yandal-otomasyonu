using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnonimGuvenlikTalebiDenetimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "YapanKullaniciId",
                table: "DegisiklikGecmisi",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Denetim_Aktor",
                table: "DegisiklikGecmisi",
                sql: "[YapanKullaniciId] IS NOT NULL OR ([VarlikTuru] = N'GuvenlikTalebi' AND [Islem] IN (N'GirisTalebi', N'ParolaSifirlamaTalebi'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [DegisiklikGecmisi] WHERE [YapanKullaniciId] IS NULL) THROW 51000, N'Anonim denetim kaydı varken geri geçiş yapılamaz; kayıt silme veya sahte aktör atama yasaktır.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Denetim_Aktor",
                table: "DegisiklikGecmisi");

            migrationBuilder.AlterColumn<Guid>(
                name: "YapanKullaniciId",
                table: "DegisiklikGecmisi",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
