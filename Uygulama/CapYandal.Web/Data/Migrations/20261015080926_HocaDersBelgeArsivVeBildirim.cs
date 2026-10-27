using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HocaDersBelgeArsivVeBildirim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DersSecimi_Onay",
                table: "DersSecimleri");

            migrationBuilder.DropIndex(
                name: "IX_DersSecimKalemleri_ListeId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_DersSecimiId_ProgramId",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_EvrakGonderimiId_ProgramId",
                table: "Belgeler");

            migrationBuilder.AddColumn<string>(
                name: "KaynakKopyasi",
                table: "GanoKontrolSonuclari",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kaynak",
                table: "GanoKontrolKayitlari",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GecmisAktarimiMi",
                table: "DersSecimleri",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KararGerekcesi",
                table: "DersSecimleri",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OgrenciKilidi",
                table: "DersSecimleri",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DuzeltmeGerekcesi",
                table: "DersSecimKalemleri",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IptalAni",
                table: "DersSecimKalemleri",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IptalEdenKullaniciId",
                table: "DersSecimKalemleri",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IptalGerekcesi",
                table: "DersSecimKalemleri",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArsivDonemId",
                table: "Belgeler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EkBelgeIstegiId",
                table: "Belgeler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "KararAni",
                table: "Belgeler",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KararGerekcesi",
                table: "Belgeler",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KararVerenKullaniciId",
                table: "Belgeler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KisiselBelgeSerisiId",
                table: "Belgeler",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KisiselSurumNo",
                table: "Belgeler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KisiselTur",
                table: "Belgeler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnayDurumu",
                table: "Belgeler",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EvrakGonderimleri_Id_ProgramId_DonemId",
                table: "EvrakGonderimleri",
                columns: new[] { "Id", "ProgramId", "DonemId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DersSecimleri_Id_ProgramId_DonemId",
                table: "DersSecimleri",
                columns: new[] { "Id", "ProgramId", "DonemId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DersSecimKalemleri_Id_ProgramId_DonemId",
                table: "DersSecimKalemleri",
                columns: new[] { "Id", "ProgramId", "DonemId" });

            migrationBuilder.CreateTable(
                name: "DersDenemeleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DersSecimKalemiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kaynak = table.Column<int>(type: "int", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersDenemeleri", x => x.Id);
                    table.CheckConstraint("CK_DersDenemesi_Kaynak", "[Kaynak] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_DersDenemeleri_DersSecimKalemleri_DersSecimKalemiId_ProgramId_DonemId",
                        columns: x => new { x.DersSecimKalemiId, x.ProgramId, x.DonemId },
                        principalTable: "DersSecimKalemleri",
                        principalColumns: new[] { "Id", "ProgramId", "DonemId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersDenemeleri_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersListeSurumleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurumNo = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    Gerekce = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KopyaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EskiKayitKopyasi = table.Column<bool>(type: "bit", nullable: false),
                    YapanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IslemAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersListeSurumleri", x => x.Id);
                    table.CheckConstraint("CK_DersListeSurumu", "[SurumNo] > 0 AND [Durum] IN (1,2,3,4) AND ISJSON([KopyaJson]) = 1");
                    table.ForeignKey(
                        name: "FK_DersListeSurumleri_DersSecimleri_ListeId",
                        column: x => x.ListeId,
                        principalTable: "DersSecimleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersListeSurumleri_Kullanicilar_YapanKullaniciId",
                        column: x => x.YapanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EkBelgeIstekleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BelgeTuru = table.Column<int>(type: "int", nullable: false),
                    IstekMetni = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsteyenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IstekAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    KararVerenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    KararAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    KararGerekcesi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkBelgeIstekleri", x => x.Id);
                    table.UniqueConstraint("AK_EkBelgeIstekleri_Id_ProgramId_DonemId_BelgeTuru", x => new { x.Id, x.ProgramId, x.DonemId, x.BelgeTuru });
                    table.CheckConstraint("CK_EkBelgeIstegi_Karar", "([Durum] IN (1,2) AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([Durum] = 3 AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL) OR ([Durum] IN (4,5) AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)");
                    table.CheckConstraint("CK_EkBelgeIstegi_TurDurum", "[BelgeTuru] IN (1,2,3,4,5) AND [Durum] IN (1,2,3,4,5) AND LEN(LTRIM(RTRIM([IstekMetni]))) > 0");
                    table.ForeignKey(
                        name: "FK_EkBelgeIstekleri_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkBelgeIstekleri_Kullanicilar_IsteyenKullaniciId",
                        column: x => x.IsteyenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkBelgeIstekleri_Kullanicilar_KararVerenKullaniciId",
                        column: x => x.KararVerenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkBelgeIstekleri_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KisiselBelgeSerileri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    OgrenciKilidi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturulmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KisiselBelgeSerileri", x => x.Id);
                    table.UniqueConstraint("AK_KisiselBelgeSerileri_Id_ProgramId_DonemId_Tur", x => new { x.Id, x.ProgramId, x.DonemId, x.Tur });
                    table.CheckConstraint("CK_KisiselBelgeSerisi_Tur", "[Tur] IN (1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_KisiselBelgeSerileri_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KisiselBelgeSerileri_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KisiselBelgeSerileri_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SohbetMesajEkleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SohbetMesajiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DepolamaAnahtari = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IcerikTuru = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BoyutBayt = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    YuklemeAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SohbetMesajEkleri", x => x.Id);
                    table.CheckConstraint("CK_SohbetMesajEki_Boyut", "[BoyutBayt] BETWEEN 1 AND 10485760");
                    table.ForeignKey(
                        name: "FK_SohbetMesajEkleri_SohbetMesajlari_SohbetMesajiId",
                        column: x => x.SohbetMesajiId,
                        principalTable: "SohbetMesajlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersDenemeSurumleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DenemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurumNo = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    GecilenDonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OnayDurumu = table.Column<int>(type: "int", nullable: false),
                    Gerekce = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    YapanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IslemAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersDenemeSurumleri", x => x.Id);
                    table.CheckConstraint("CK_DersDenemeSurumu_Durum", "[SurumNo] > 0 AND [Durum] IN (1,2,3,4,5,6) AND [OnayDurumu] IN (1,2,3,4)");
                    table.CheckConstraint("CK_DersDenemeSurumu_GecilenDonem", "([Durum] = 3 AND [GecilenDonemId] IS NOT NULL) OR ([Durum] <> 3 AND [GecilenDonemId] IS NULL)");
                    table.CheckConstraint("CK_DersDenemeSurumu_Gerekce", "[OnayDurumu] NOT IN (3,4) OR ([Gerekce] IS NOT NULL AND LEN(LTRIM(RTRIM([Gerekce]))) > 0)");
                    table.ForeignKey(
                        name: "FK_DersDenemeSurumleri_DersDenemeleri_DenemeId",
                        column: x => x.DenemeId,
                        principalTable: "DersDenemeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersDenemeSurumleri_Donemler_GecilenDonemId",
                        column: x => x.GecilenDonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersDenemeSurumleri_Kullanicilar_YapanKullaniciId",
                        column: x => x.YapanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersSecimi_Onay",
                table: "DersSecimleri",
                sql: "([Durum] = 1 AND [OnaylayanKullaniciId] IS NULL AND [OnayAni] IS NULL AND [KararGerekcesi] IS NULL) OR ([Durum] = 2 AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL) OR ([Durum] IN (3,4) AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_IptalEdenKullaniciId",
                table: "DersSecimKalemleri",
                column: "IptalEdenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_ListeId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "ListeId", "DersId" },
                unique: true,
                filter: "[IptalAni] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersKalemi_Iptal",
                table: "DersSecimKalemleri",
                sql: "([IptalAni] IS NULL AND [IptalEdenKullaniciId] IS NULL AND [IptalGerekcesi] IS NULL) OR ([IptalAni] IS NOT NULL AND [IptalEdenKullaniciId] IS NOT NULL AND [IptalGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([IptalGerekcesi]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_DersSecimiId_ProgramId_ArsivDonemId",
                table: "Belgeler",
                columns: new[] { "DersSecimiId", "ProgramId", "ArsivDonemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_EkBelgeIstegiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler",
                columns: new[] { "EkBelgeIstegiId", "ProgramId", "ArsivDonemId", "KisiselTur" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_EvrakGonderimiId_ProgramId_ArsivDonemId",
                table: "Belgeler",
                columns: new[] { "EvrakGonderimiId", "ProgramId", "ArsivDonemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_KararVerenKullaniciId",
                table: "Belgeler",
                column: "KararVerenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_KisiselBelgeSerisiId_KisiselSurumNo",
                table: "Belgeler",
                columns: new[] { "KisiselBelgeSerisiId", "KisiselSurumNo" },
                unique: true,
                filter: "[KisiselBelgeSerisiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_KisiselBelgeSerisiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler",
                columns: new[] { "KisiselBelgeSerisiId", "ProgramId", "ArsivDonemId", "KisiselTur" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_ArsivSerisi",
                table: "Belgeler",
                sql: "([KisiselBelgeSerisiId] IS NULL AND [KisiselSurumNo] IS NULL AND [KisiselTur] IS NULL AND [ArsivDonemId] IS NULL AND [EkBelgeIstegiId] IS NULL) OR ([Kapsam] = 2 AND [KisiselBelgeSerisiId] IS NOT NULL AND [KisiselSurumNo] IS NOT NULL AND [KisiselSurumNo] > 0 AND [KisiselTur] IS NOT NULL AND [KisiselTur] IN (1,2,3,4,5) AND [ArsivDonemId] IS NOT NULL AND [ProgramId] IS NOT NULL AND [OnayDurumu] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Belge_OnayKarari",
                table: "Belgeler",
                sql: "([OnayDurumu] IS NULL AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([Kapsam] = 2 AND [OnayDurumu] IS NOT NULL AND (([OnayDurumu] = 1 AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([OnayDurumu] = 2 AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL) OR ([OnayDurumu] IN (3,4) AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)))");

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeleri_DersSecimKalemiId",
                table: "DersDenemeleri",
                column: "DersSecimKalemiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeleri_DersSecimKalemiId_ProgramId_DonemId",
                table: "DersDenemeleri",
                columns: new[] { "DersSecimKalemiId", "ProgramId", "DonemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeleri_OlusturanKullaniciId",
                table: "DersDenemeleri",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeleri_ProgramId_DonemId",
                table: "DersDenemeleri",
                columns: new[] { "ProgramId", "DonemId" });

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeSurumleri_DenemeId_SurumNo",
                table: "DersDenemeSurumleri",
                columns: new[] { "DenemeId", "SurumNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeSurumleri_GecilenDonemId",
                table: "DersDenemeSurumleri",
                column: "GecilenDonemId");

            migrationBuilder.CreateIndex(
                name: "IX_DersDenemeSurumleri_YapanKullaniciId",
                table: "DersDenemeSurumleri",
                column: "YapanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersListeSurumleri_ListeId_SurumNo",
                table: "DersListeSurumleri",
                columns: new[] { "ListeId", "SurumNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersListeSurumleri_YapanKullaniciId",
                table: "DersListeSurumleri",
                column: "YapanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_EkBelgeIstekleri_DonemId",
                table: "EkBelgeIstekleri",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_EkBelgeIstekleri_IsteyenKullaniciId",
                table: "EkBelgeIstekleri",
                column: "IsteyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_EkBelgeIstekleri_KararVerenKullaniciId",
                table: "EkBelgeIstekleri",
                column: "KararVerenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_EkBelgeIstekleri_ProgramId_DonemId_Durum",
                table: "EkBelgeIstekleri",
                columns: new[] { "ProgramId", "DonemId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_KisiselBelgeSerileri_DonemId",
                table: "KisiselBelgeSerileri",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_KisiselBelgeSerileri_OlusturanKullaniciId",
                table: "KisiselBelgeSerileri",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_KisiselBelgeSerileri_ProgramId",
                table: "KisiselBelgeSerileri",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_SohbetMesajEkleri_SohbetMesajiId",
                table: "SohbetMesajEkleri",
                column: "SohbetMesajiId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_DersSecimleri_DersSecimiId_ProgramId_ArsivDonemId",
                table: "Belgeler",
                columns: new[] { "DersSecimiId", "ProgramId", "ArsivDonemId" },
                principalTable: "DersSecimleri",
                principalColumns: new[] { "Id", "ProgramId", "DonemId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_EkBelgeIstekleri_EkBelgeIstegiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler",
                columns: new[] { "EkBelgeIstegiId", "ProgramId", "ArsivDonemId", "KisiselTur" },
                principalTable: "EkBelgeIstekleri",
                principalColumns: new[] { "Id", "ProgramId", "DonemId", "BelgeTuru" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_EvrakGonderimleri_EvrakGonderimiId_ProgramId_ArsivDonemId",
                table: "Belgeler",
                columns: new[] { "EvrakGonderimiId", "ProgramId", "ArsivDonemId" },
                principalTable: "EvrakGonderimleri",
                principalColumns: new[] { "Id", "ProgramId", "DonemId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_KisiselBelgeSerileri_KisiselBelgeSerisiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler",
                columns: new[] { "KisiselBelgeSerisiId", "ProgramId", "ArsivDonemId", "KisiselTur" },
                principalTable: "KisiselBelgeSerileri",
                principalColumns: new[] { "Id", "ProgramId", "DonemId", "Tur" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Belgeler_Kullanicilar_KararVerenKullaniciId",
                table: "Belgeler",
                column: "KararVerenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DersSecimKalemleri_Kullanicilar_IptalEdenKullaniciId",
                table: "DersSecimKalemleri",
                column: "IptalEdenKullaniciId",
                principalTable: "Kullanicilar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_DersSecimleri_DersSecimiId_ProgramId_ArsivDonemId",
                table: "Belgeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_EkBelgeIstekleri_EkBelgeIstegiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_EvrakGonderimleri_EvrakGonderimiId_ProgramId_ArsivDonemId",
                table: "Belgeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_KisiselBelgeSerileri_KisiselBelgeSerisiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler");

            migrationBuilder.DropForeignKey(
                name: "FK_Belgeler_Kullanicilar_KararVerenKullaniciId",
                table: "Belgeler");

            migrationBuilder.DropForeignKey(
                name: "FK_DersSecimKalemleri_Kullanicilar_IptalEdenKullaniciId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropTable(
                name: "DersDenemeSurumleri");

            migrationBuilder.DropTable(
                name: "DersListeSurumleri");

            migrationBuilder.DropTable(
                name: "EkBelgeIstekleri");

            migrationBuilder.DropTable(
                name: "KisiselBelgeSerileri");

            migrationBuilder.DropTable(
                name: "SohbetMesajEkleri");

            migrationBuilder.DropTable(
                name: "DersDenemeleri");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EvrakGonderimleri_Id_ProgramId_DonemId",
                table: "EvrakGonderimleri");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DersSecimleri_Id_ProgramId_DonemId",
                table: "DersSecimleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DersSecimi_Onay",
                table: "DersSecimleri");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DersSecimKalemleri_Id_ProgramId_DonemId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropIndex(
                name: "IX_DersSecimKalemleri_IptalEdenKullaniciId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropIndex(
                name: "IX_DersSecimKalemleri_ListeId_DersId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DersKalemi_Iptal",
                table: "DersSecimKalemleri");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_DersSecimiId_ProgramId_ArsivDonemId",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_EkBelgeIstegiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_EvrakGonderimiId_ProgramId_ArsivDonemId",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_KararVerenKullaniciId",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_KisiselBelgeSerisiId_KisiselSurumNo",
                table: "Belgeler");

            migrationBuilder.DropIndex(
                name: "IX_Belgeler_KisiselBelgeSerisiId_ProgramId_ArsivDonemId_KisiselTur",
                table: "Belgeler");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_ArsivSerisi",
                table: "Belgeler");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Belge_OnayKarari",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KaynakKopyasi",
                table: "GanoKontrolSonuclari");

            migrationBuilder.DropColumn(
                name: "Kaynak",
                table: "GanoKontrolKayitlari");

            migrationBuilder.DropColumn(
                name: "GecmisAktarimiMi",
                table: "DersSecimleri");

            migrationBuilder.DropColumn(
                name: "KararGerekcesi",
                table: "DersSecimleri");

            migrationBuilder.DropColumn(
                name: "OgrenciKilidi",
                table: "DersSecimleri");

            migrationBuilder.DropColumn(
                name: "DuzeltmeGerekcesi",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "IptalAni",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "IptalEdenKullaniciId",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "IptalGerekcesi",
                table: "DersSecimKalemleri");

            migrationBuilder.DropColumn(
                name: "ArsivDonemId",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "EkBelgeIstegiId",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KararAni",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KararGerekcesi",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KararVerenKullaniciId",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KisiselBelgeSerisiId",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KisiselSurumNo",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "KisiselTur",
                table: "Belgeler");

            migrationBuilder.DropColumn(
                name: "OnayDurumu",
                table: "Belgeler");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DersSecimi_Onay",
                table: "DersSecimleri",
                sql: "([Durum] = 1 AND [OnaylayanKullaniciId] IS NULL AND [OnayAni] IS NULL) OR ([Durum] = 2 AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_ListeId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "ListeId", "DersId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_DersSecimiId_ProgramId",
                table: "Belgeler",
                columns: new[] { "DersSecimiId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_EvrakGonderimiId_ProgramId",
                table: "Belgeler",
                columns: new[] { "EvrakGonderimiId", "ProgramId" });
        }
    }
}
