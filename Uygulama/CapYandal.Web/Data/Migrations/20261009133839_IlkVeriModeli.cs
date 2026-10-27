using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapYandal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class IlkVeriModeli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dersler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    HedefBolum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KaynakAnaDal = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Kod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Akts = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dersler", x => x.Id);
                    table.CheckConstraint("CK_Ders_Akts", "[Akts] >= 0");
                    table.CheckConstraint("CK_Ders_Kaynak", "([Tur] = 1 AND [KaynakAnaDal] IS NOT NULL AND LEN(LTRIM(RTRIM([KaynakAnaDal]))) > 0) OR ([Tur] = 2 AND [KaynakAnaDal] IS NULL)");
                    table.CheckConstraint("CK_Ders_Tur", "[Tur] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Donemler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AkademikYilBaslangici = table.Column<int>(type: "int", nullable: false),
                    Yariyil = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    BitisTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    IslemDonemiMi = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donemler", x => x.Id);
                    table.CheckConstraint("CK_Donem_Tarih", "[BaslangicTarihi] <= [BitisTarihi]");
                    table.CheckConstraint("CK_Donem_Yariyil", "[Yariyil] IN (1, 2)");
                    table.CheckConstraint("CK_Donem_Yil", "[AkademikYilBaslangici] BETWEEN 1 AND 9998");
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OgrenciNo = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Soyad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KisiselEposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BekleyenKisiselEposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    KisiselEpostaOnayliMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IlkGirisAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SonGirisAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ParolaDegisikligiZorunlu = table.Column<bool>(type: "bit", nullable: false),
                    GeciciParolaBitisAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    KurumsalEposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    KurumsalEpostaOnayliMi = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                    table.CheckConstraint("CK_Kullanici_KisiselEpostaOnayi", "[KisiselEpostaOnayliMi] = 0 OR [KisiselEposta] IS NOT NULL");
                    table.CheckConstraint("CK_Kullanici_OgrenciGeciciParolaYok", "[OgrenciNo] IS NULL OR ([ParolaDegisikligiZorunlu] = 0 AND [GeciciParolaBitisAni] IS NULL)");
                    table.CheckConstraint("CK_Kullanici_OgrenciNo", "[OgrenciNo] IS NULL OR (DATALENGTH([OgrenciNo]) = 18 AND [OgrenciNo] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9]%' AND [UserName] IS NOT NULL AND DATALENGTH([UserName]) = 18 AND [UserName] COLLATE Latin1_General_100_BIN2 = [OgrenciNo] COLLATE Latin1_General_100_BIN2)");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DegisiklikGecmisi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VarlikTuru = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    VarlikAnahtari = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Islem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OncekiDeger = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YeniDeger = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YapanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IslemAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DegisiklikGecmisi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DegisiklikGecmisi_Kullanicilar_YapanKullaniciId",
                        column: x => x.YapanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersSablonlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    HedefBolum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KaynakAnaDal = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturulmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersSablonlari", x => x.Id);
                    table.CheckConstraint("CK_Sablon_Kaynak", "([Tur] = 1 AND [KaynakAnaDal] IS NOT NULL AND LEN(LTRIM(RTRIM([KaynakAnaDal]))) > 0) OR ([Tur] = 2 AND [KaynakAnaDal] IS NULL)");
                    table.CheckConstraint("CK_Sablon_Tur", "[Tur] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_DersSablonlari_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EpostaGonderimKayitlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Saglayici = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Alici = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Konu = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    GonderimAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    HataOzeti = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpostaGonderimKayitlari", x => x.Id);
                    table.CheckConstraint("CK_Eposta_Durum", "[Durum] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_EpostaGonderimKayitlari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KosulKumeleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramTuru = table.Column<int>(type: "int", nullable: false),
                    Kapsam = table.Column<int>(type: "int", nullable: false),
                    YururlukBaslangici = table.Column<DateOnly>(type: "date", nullable: false),
                    YururlukBitisi = table.Column<DateOnly>(type: "date", nullable: true),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OlusturulmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KosulKumeleri", x => x.Id);
                    table.CheckConstraint("CK_KosulKumesi_Kapsam", "[ProgramTuru] IN (1, 2) AND [Kapsam] IN (1, 2, 3)");
                    table.CheckConstraint("CK_KosulKumesi_Tarih", "[YururlukBitisi] IS NULL OR [YururlukBaslangici] <= [YururlukBitisi]");
                    table.ForeignKey(
                        name: "FK_KosulKumeleri_Kullanicilar_OlusturanKullaniciId",
                        column: x => x.OlusturanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OgrenciProgramlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OgrenciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    HedefBolum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KaynakAnaDal = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BaslangicTarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    DegisiklikAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OgrenciProgramlari", x => x.Id);
                    table.CheckConstraint("CK_Program_Hedef", "[HedefBolum] = N'Bilgisayar Mühendisliği'");
                    table.CheckConstraint("CK_Program_Tur", "[Tur] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_OgrenciProgramlari_Kullanicilar_OgrenciId",
                        column: x => x.OgrenciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SohbetMesajlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OgrenciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GonderenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GonderimAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OkunmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SohbetMesajlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SohbetMesajlari_Kullanicilar_GonderenKullaniciId",
                        column: x => x.GonderenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SohbetMesajlari_Kullanicilar_OgrenciId",
                        column: x => x.OgrenciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SablonDersleri",
                columns: table => new
                {
                    SablonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MufredatYariyili = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SablonDersleri", x => new { x.SablonId, x.DersId });
                    table.CheckConstraint("CK_SablonDersi_Tur", "[Tur] IN (1, 2, 3)");
                    table.CheckConstraint("CK_SablonDersi_Yariyil", "[MufredatYariyili] >= 1");
                    table.ForeignKey(
                        name: "FK_SablonDersleri_DersSablonlari_SablonId",
                        column: x => x.SablonId,
                        principalTable: "DersSablonlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SablonDersleri_Dersler_DersId",
                        column: x => x.DersId,
                        principalTable: "Dersler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecmeliGereklilikleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SablonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MufredatYariyili = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    GerekenSayi = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecmeliGereklilikleri", x => x.Id);
                    table.CheckConstraint("CK_Secmeli_Sayi", "[GerekenSayi] >= 0 AND [MufredatYariyili] >= 1");
                    table.CheckConstraint("CK_Secmeli_Tur", "[Tur] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_SecmeliGereklilikleri_DersSablonlari_SablonId",
                        column: x => x.SablonId,
                        principalTable: "DersSablonlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KosulDegerleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KosulKumesiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KuralKodu = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalEsik = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    AltEsik = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    ReferansDegeri = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    IzinSayisi = table.Column<int>(type: "int", nullable: true),
                    IzinTuru = table.Column<int>(type: "int", nullable: true),
                    ArdisikDonemSayisi = table.Column<int>(type: "int", nullable: true),
                    DegerlendirmeTuru = table.Column<int>(type: "int", nullable: false),
                    EtkinMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KosulDegerleri", x => x.Id);
                    table.UniqueConstraint("AK_KosulDegerleri_KosulKumesiId_KuralKodu", x => new { x.KosulKumesiId, x.KuralKodu });
                    table.CheckConstraint("CK_Kosul_Esikler", "([NormalEsik] IS NULL OR [NormalEsik] BETWEEN 0 AND 4) AND ([AltEsik] IS NULL OR [AltEsik] BETWEEN 0 AND 4) AND ([AltEsik] IS NULL OR ([NormalEsik] IS NOT NULL AND [AltEsik] <= [NormalEsik]))");
                    table.CheckConstraint("CK_Kosul_Izin", "([IzinSayisi] IS NULL OR [IzinSayisi] >= 0) AND ([ArdisikDonemSayisi] IS NULL OR [ArdisikDonemSayisi] >= 1) AND ([IzinTuru] IS NULL OR [IzinTuru] IN (1, 2))");
                    table.CheckConstraint("CK_Kosul_Kullanim", "[DegerlendirmeTuru] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_KosulDegerleri_KosulKumeleri_KosulKumesiId",
                        column: x => x.KosulKumesiId,
                        principalTable: "KosulKumeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersSecimleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SablonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GonderimAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GonderenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    OnaylayanKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OnayAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersSecimleri", x => x.Id);
                    table.UniqueConstraint("AK_DersSecimleri_Id_ProgramId", x => new { x.Id, x.ProgramId });
                    table.UniqueConstraint("AK_DersSecimleri_Id_ProgramId_DonemId_SablonId", x => new { x.Id, x.ProgramId, x.DonemId, x.SablonId });
                    table.CheckConstraint("CK_DersSecimi_Onay", "([Durum] = 1 AND [OnaylayanKullaniciId] IS NULL AND [OnayAni] IS NULL) OR ([Durum] = 2 AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DersSecimleri_DersSablonlari_SablonId",
                        column: x => x.SablonId,
                        principalTable: "DersSablonlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimleri_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimleri_Kullanicilar_GonderenKullaniciId",
                        column: x => x.GonderenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimleri_Kullanicilar_OnaylayanKullaniciId",
                        column: x => x.OnaylayanKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimleri_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvrakGonderimleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GonderenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GonderimAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvrakGonderimleri", x => x.Id);
                    table.UniqueConstraint("AK_EvrakGonderimleri_Id_ProgramId", x => new { x.Id, x.ProgramId });
                    table.ForeignKey(
                        name: "FK_EvrakGonderimleri_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvrakGonderimleri_Kullanicilar_GonderenKullaniciId",
                        column: x => x.GonderenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvrakGonderimleri_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GanoKontrolKayitlari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kapsam = table.Column<int>(type: "int", nullable: false),
                    Gano = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    GirenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuzeltenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DegisiklikAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GanoKontrolKayitlari", x => x.Id);
                    table.CheckConstraint("CK_Gano_Deger", "[Gano] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_Gano_Kapsam", "[Kapsam] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_GanoKontrolKayitlari_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanoKontrolKayitlari_Kullanicilar_DuzeltenKullaniciId",
                        column: x => x.DuzeltenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanoKontrolKayitlari_Kullanicilar_GirenKullaniciId",
                        column: x => x.GirenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanoKontrolKayitlari_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SablonDonemDersleri",
                columns: table => new
                {
                    SablonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecimeAcikMi = table.Column<bool>(type: "bit", nullable: false),
                    OgretimElemani = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DegistirenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DegisiklikAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SablonDonemDersleri", x => new { x.SablonId, x.DonemId, x.DersId });
                    table.ForeignKey(
                        name: "FK_SablonDonemDersleri_Donemler_DonemId",
                        column: x => x.DonemId,
                        principalTable: "Donemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SablonDonemDersleri_Kullanicilar_DegistirenKullaniciId",
                        column: x => x.DegistirenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SablonDonemDersleri_SablonDersleri_SablonId_DersId",
                        columns: x => new { x.SablonId, x.DersId },
                        principalTable: "SablonDersleri",
                        principalColumns: new[] { "SablonId", "DersId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Belgeler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kapsam = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DersSecimiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvrakGonderimiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    YukleyenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    YuklemeAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IcerikTuru = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BoyutBayt = table.Column<long>(type: "bigint", nullable: true),
                    DepolamaAnahtari = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResmiBaglanti = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IcerikDurumu = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IcerikKaldirilmaAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    KaldiranKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Belgeler", x => x.Id);
                    table.CheckConstraint("CK_Belge_Baglam", "([Kapsam] = 1 AND [ProgramId] IS NULL AND [DersSecimiId] IS NULL AND [EvrakGonderimiId] IS NULL) OR ([Kapsam] = 2 AND [ProgramId] IS NOT NULL AND (([DersSecimiId] IS NOT NULL AND [EvrakGonderimiId] IS NULL) OR ([DersSecimiId] IS NULL AND [EvrakGonderimiId] IS NOT NULL)))");
                    table.CheckConstraint("CK_Belge_IcerikDurumu", "([IcerikDurumu] IN (1, 2) AND [IcerikKaldirilmaAni] IS NULL AND [KaldiranKullaniciId] IS NULL) OR ([IcerikDurumu] = 3 AND [IcerikKaldirilmaAni] IS NOT NULL AND [KaldiranKullaniciId] IS NOT NULL)");
                    table.CheckConstraint("CK_Belge_Kaynak", "([ResmiBaglanti] IS NOT NULL AND [Kapsam] = 1 AND [ResmiBaglanti] LIKE N'https://%' AND [DepolamaAnahtari] IS NULL AND [DosyaAdi] IS NULL AND [IcerikTuru] IS NULL AND [BoyutBayt] IS NULL) OR ([ResmiBaglanti] IS NULL AND [DepolamaAnahtari] IS NOT NULL AND [DosyaAdi] IS NOT NULL AND [IcerikTuru] IS NOT NULL AND [BoyutBayt] IS NOT NULL AND [BoyutBayt] >= 0)");
                    table.ForeignKey(
                        name: "FK_Belgeler_DersSecimleri_DersSecimiId_ProgramId",
                        columns: x => new { x.DersSecimiId, x.ProgramId },
                        principalTable: "DersSecimleri",
                        principalColumns: new[] { "Id", "ProgramId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Belgeler_EvrakGonderimleri_EvrakGonderimiId_ProgramId",
                        columns: x => new { x.EvrakGonderimiId, x.ProgramId },
                        principalTable: "EvrakGonderimleri",
                        principalColumns: new[] { "Id", "ProgramId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Belgeler_Kullanicilar_KaldiranKullaniciId",
                        column: x => x.KaldiranKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Belgeler_Kullanicilar_YukleyenKullaniciId",
                        column: x => x.YukleyenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Belgeler_OgrenciProgramlari_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "OgrenciProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GanoKontrolSonuclari",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GanoKaydiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KosulKumesiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KuralKodu = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SonucKodu = table.Column<int>(type: "int", nullable: false),
                    Renk = table.Column<int>(type: "int", nullable: true),
                    GecmisEksikMi = table.Column<bool>(type: "bit", nullable: false),
                    KullanilanNormalEsik = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    KullanilanAltEsik = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    KullanilanIzinSayisi = table.Column<int>(type: "int", nullable: true),
                    KullanilanDusukDonemSayisi = table.Column<int>(type: "int", nullable: true),
                    KontrolEdenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KontrolAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GanoKontrolSonuclari", x => x.Id);
                    table.CheckConstraint("CK_GanoSonuc_Izin", "([KullanilanIzinSayisi] IS NULL OR [KullanilanIzinSayisi] >= 0) AND ([KullanilanDusukDonemSayisi] IS NULL OR [KullanilanDusukDonemSayisi] >= 0)");
                    table.CheckConstraint("CK_GanoSonuc_Renk", "([Renk] IS NOT NULL AND (([SonucKodu] = 1 AND [Renk] = 1) OR ([SonucKodu] = 2 AND [Renk] = 2) OR ([SonucKodu] = 3 AND [Renk] = 3))) OR ([SonucKodu] IN (4, 5) AND [Renk] IS NULL)");
                    table.ForeignKey(
                        name: "FK_GanoKontrolSonuclari_GanoKontrolKayitlari_GanoKaydiId",
                        column: x => x.GanoKaydiId,
                        principalTable: "GanoKontrolKayitlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanoKontrolSonuclari_KosulDegerleri_KosulKumesiId_KuralKodu",
                        columns: x => new { x.KosulKumesiId, x.KuralKodu },
                        principalTable: "KosulDegerleri",
                        principalColumns: new[] { "KosulKumesiId", "KuralKodu" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanoKontrolSonuclari_Kullanicilar_KontrolEdenKullaniciId",
                        column: x => x.KontrolEdenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DersSecimKalemleri",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SablonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DersKoduKopyasi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DersAdiKopyasi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AktsKopyasi = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    OgretimElemaniKopyasi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EkleyenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EklenmeAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DuzeltenKullaniciId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DuzeltmeAni = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DersSecimKalemleri", x => x.Id);
                    table.CheckConstraint("CK_DersKalemi_Akts", "[AktsKopyasi] >= 0");
                    table.ForeignKey(
                        name: "FK_DersSecimKalemleri_DersSecimleri_ListeId_ProgramId_DonemId_SablonId",
                        columns: x => new { x.ListeId, x.ProgramId, x.DonemId, x.SablonId },
                        principalTable: "DersSecimleri",
                        principalColumns: new[] { "Id", "ProgramId", "DonemId", "SablonId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimKalemleri_Kullanicilar_DuzeltenKullaniciId",
                        column: x => x.DuzeltenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimKalemleri_Kullanicilar_EkleyenKullaniciId",
                        column: x => x.EkleyenKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DersSecimKalemleri_SablonDonemDersleri_SablonId_DonemId_DersId",
                        columns: x => new { x.SablonId, x.DonemId, x.DersId },
                        principalTable: "SablonDonemDersleri",
                        principalColumns: new[] { "SablonId", "DonemId", "DersId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_DersSecimiId_ProgramId",
                table: "Belgeler",
                columns: new[] { "DersSecimiId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_EvrakGonderimiId_ProgramId",
                table: "Belgeler",
                columns: new[] { "EvrakGonderimiId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_KaldiranKullaniciId",
                table: "Belgeler",
                column: "KaldiranKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_ProgramId",
                table: "Belgeler",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Belgeler_YukleyenKullaniciId",
                table: "Belgeler",
                column: "YukleyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DegisiklikGecmisi_VarlikTuru_VarlikAnahtari_IslemAni",
                table: "DegisiklikGecmisi",
                columns: new[] { "VarlikTuru", "VarlikAnahtari", "IslemAni" });

            migrationBuilder.CreateIndex(
                name: "IX_DegisiklikGecmisi_YapanKullaniciId",
                table: "DegisiklikGecmisi",
                column: "YapanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Dersler_Tur_HedefBolum_KaynakAnaDal_Kod",
                table: "Dersler",
                columns: new[] { "Tur", "HedefBolum", "KaynakAnaDal", "Kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_OlusturanKullaniciId",
                table: "DersSablonlari",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSablonlari_Tur_HedefBolum_KaynakAnaDal",
                table: "DersSablonlari",
                columns: new[] { "Tur", "HedefBolum", "KaynakAnaDal" },
                unique: true,
                filter: "[EtkinMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_DuzeltenKullaniciId",
                table: "DersSecimKalemleri",
                column: "DuzeltenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_EkleyenKullaniciId",
                table: "DersSecimKalemleri",
                column: "EkleyenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_ListeId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "ListeId", "DersId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_ListeId_ProgramId_DonemId_SablonId",
                table: "DersSecimKalemleri",
                columns: new[] { "ListeId", "ProgramId", "DonemId", "SablonId" });

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimKalemleri_SablonId_DonemId_DersId",
                table: "DersSecimKalemleri",
                columns: new[] { "SablonId", "DonemId", "DersId" });

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimleri_DonemId",
                table: "DersSecimleri",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimleri_GonderenKullaniciId",
                table: "DersSecimleri",
                column: "GonderenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimleri_OnaylayanKullaniciId",
                table: "DersSecimleri",
                column: "OnaylayanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimleri_ProgramId_DonemId",
                table: "DersSecimleri",
                columns: new[] { "ProgramId", "DonemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DersSecimleri_SablonId",
                table: "DersSecimleri",
                column: "SablonId");

            migrationBuilder.CreateIndex(
                name: "IX_Donemler_AkademikYilBaslangici_Yariyil",
                table: "Donemler",
                columns: new[] { "AkademikYilBaslangici", "Yariyil" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donemler_IslemDonemiMi",
                table: "Donemler",
                column: "IslemDonemiMi",
                unique: true,
                filter: "[IslemDonemiMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EpostaGonderimKayitlari_KullaniciId",
                table: "EpostaGonderimKayitlari",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_EvrakGonderimleri_DonemId",
                table: "EvrakGonderimleri",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvrakGonderimleri_GonderenKullaniciId",
                table: "EvrakGonderimleri",
                column: "GonderenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_EvrakGonderimleri_ProgramId",
                table: "EvrakGonderimleri",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolKayitlari_DonemId",
                table: "GanoKontrolKayitlari",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolKayitlari_DuzeltenKullaniciId",
                table: "GanoKontrolKayitlari",
                column: "DuzeltenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolKayitlari_GirenKullaniciId",
                table: "GanoKontrolKayitlari",
                column: "GirenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolKayitlari_ProgramId_DonemId_Kapsam",
                table: "GanoKontrolKayitlari",
                columns: new[] { "ProgramId", "DonemId", "Kapsam" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolSonuclari_GanoKaydiId_KuralKodu",
                table: "GanoKontrolSonuclari",
                columns: new[] { "GanoKaydiId", "KuralKodu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolSonuclari_KontrolEdenKullaniciId",
                table: "GanoKontrolSonuclari",
                column: "KontrolEdenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_GanoKontrolSonuclari_KosulKumesiId_KuralKodu",
                table: "GanoKontrolSonuclari",
                columns: new[] { "KosulKumesiId", "KuralKodu" });

            migrationBuilder.CreateIndex(
                name: "IX_KosulKumeleri_OlusturanKullaniciId",
                table: "KosulKumeleri",
                column: "OlusturanKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_KosulKumeleri_ProgramTuru_Kapsam_YururlukBaslangici_YururlukBitisi",
                table: "KosulKumeleri",
                columns: new[] { "ProgramTuru", "Kapsam", "YururlukBaslangici", "YururlukBitisi" });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Kullanicilar",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_OgrenciNo",
                table: "Kullanicilar",
                column: "OgrenciNo",
                unique: true,
                filter: "[OgrenciNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Kullanicilar",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OgrenciProgramlari_OgrenciId_HedefBolum",
                table: "OgrenciProgramlari",
                columns: new[] { "OgrenciId", "HedefBolum" },
                unique: true,
                filter: "[AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OgrenciProgramlari_OgrenciId_Tur",
                table: "OgrenciProgramlari",
                columns: new[] { "OgrenciId", "Tur" },
                unique: true,
                filter: "[AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SablonDersleri_DersId",
                table: "SablonDersleri",
                column: "DersId");

            migrationBuilder.CreateIndex(
                name: "IX_SablonDonemDersleri_DegistirenKullaniciId",
                table: "SablonDonemDersleri",
                column: "DegistirenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_SablonDonemDersleri_DonemId",
                table: "SablonDonemDersleri",
                column: "DonemId");

            migrationBuilder.CreateIndex(
                name: "IX_SablonDonemDersleri_SablonId_DersId",
                table: "SablonDonemDersleri",
                columns: new[] { "SablonId", "DersId" });

            migrationBuilder.CreateIndex(
                name: "IX_SecmeliGereklilikleri_SablonId_MufredatYariyili_Tur",
                table: "SecmeliGereklilikleri",
                columns: new[] { "SablonId", "MufredatYariyili", "Tur" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SohbetMesajlari_GonderenKullaniciId",
                table: "SohbetMesajlari",
                column: "GonderenKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_SohbetMesajlari_OgrenciId_GonderimAni",
                table: "SohbetMesajlari",
                columns: new[] { "OgrenciId", "GonderimAni" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Belgeler");

            migrationBuilder.DropTable(
                name: "DegisiklikGecmisi");

            migrationBuilder.DropTable(
                name: "DersSecimKalemleri");

            migrationBuilder.DropTable(
                name: "EpostaGonderimKayitlari");

            migrationBuilder.DropTable(
                name: "GanoKontrolSonuclari");

            migrationBuilder.DropTable(
                name: "SecmeliGereklilikleri");

            migrationBuilder.DropTable(
                name: "SohbetMesajlari");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "EvrakGonderimleri");

            migrationBuilder.DropTable(
                name: "DersSecimleri");

            migrationBuilder.DropTable(
                name: "SablonDonemDersleri");

            migrationBuilder.DropTable(
                name: "GanoKontrolKayitlari");

            migrationBuilder.DropTable(
                name: "KosulDegerleri");

            migrationBuilder.DropTable(
                name: "SablonDersleri");

            migrationBuilder.DropTable(
                name: "Donemler");

            migrationBuilder.DropTable(
                name: "OgrenciProgramlari");

            migrationBuilder.DropTable(
                name: "KosulKumeleri");

            migrationBuilder.DropTable(
                name: "DersSablonlari");

            migrationBuilder.DropTable(
                name: "Dersler");

            migrationBuilder.DropTable(
                name: "Kullanicilar");
        }
    }
}
