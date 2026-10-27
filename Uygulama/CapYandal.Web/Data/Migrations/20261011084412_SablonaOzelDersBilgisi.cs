using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SablonaOzelDersBilgisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ad",
                table: "SablonDersleri",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Akts",
                table: "SablonDersleri",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kod",
                table: "SablonDersleri",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DersSablonlari",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql("UPDATE sd SET sd.[Kod] = d.[Kod], sd.[Ad] = d.[Ad], sd.[Akts] = d.[Akts] FROM [SablonDersleri] sd INNER JOIN [Dersler] d ON d.[Id] = sd.[DersId];");

            migrationBuilder.CreateIndex(
                name: "IX_SablonDersleri_SablonId_Kod",
                table: "SablonDersleri",
                columns: new[] { "SablonId", "Kod" },
                unique: true,
                filter: "[EtkinMi] = 1 AND [Kod] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SablonDersi_Akts",
                table: "SablonDersleri",
                sql: "[Akts] IS NULL OR [Akts] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [SablonDersleri] sd JOIN [Dersler] d ON d.[Id] = sd.[DersId] WHERE sd.[Kod] <> d.[Kod] OR sd.[Ad] <> d.[Ad] OR sd.[Akts] <> d.[Akts]) THROW 50000, 'Sablona ozel bilgiler korunmalidir; geri donus icin once inceleme gerekir.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_SablonDersleri_SablonId_Kod",
                table: "SablonDersleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SablonDersi_Akts",
                table: "SablonDersleri");

            migrationBuilder.DropColumn(
                name: "Ad",
                table: "SablonDersleri");

            migrationBuilder.DropColumn(
                name: "Akts",
                table: "SablonDersleri");

            migrationBuilder.DropColumn(
                name: "Kod",
                table: "SablonDersleri");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DersSablonlari");
        }
    }
}
