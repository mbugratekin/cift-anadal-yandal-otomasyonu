using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

internal static class DersConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DersSablonu>(entity =>
        {
            entity.ToTable("DersSablonlari", table =>
            {
                table.HasCheckConstraint("CK_Sablon_Tur", "[Tur] IN (1, 2)");
                table.HasCheckConstraint("CK_Sablon_MufredatNo", "[MufredatNo] >= 1");
                table.HasCheckConstraint("CK_Sablon_Kaynak", "([Tur] = 1 AND [KaynakAnaDal] IS NOT NULL AND LEN(LTRIM(RTRIM([KaynakAnaDal]))) > 0) OR ([Tur] = 2 AND [KaynakAnaDal] IS NULL)");
                table.HasCheckConstraint("CK_Sablon_MufredatAdi_Bicim", "[MufredatAdi] IS NULL OR (LEN(LTRIM(RTRIM([MufredatAdi]))) > 0 AND DATALENGTH([MufredatAdi]) = DATALENGTH(LTRIM(RTRIM([MufredatAdi]))))");
            });
            entity.Property(template => template.HedefBolum).HasMaxLength(128).IsRequired();
            entity.Property(template => template.KaynakAnaDal).HasMaxLength(128);
            entity.Property(template => template.Ad).HasMaxLength(200).IsRequired();
            entity.Property(template => template.MufredatAdi).HasMaxLength(100);
            entity.Property(template => template.RowVersion).IsRowVersion();
            entity.HasIndex(template => new { template.Tur, template.HedefBolum, template.KaynakAnaDal, template.MufredatNo }).IsUnique().HasFilter(null);
            entity.HasIndex(template => new { template.Tur, template.HedefBolum, template.KaynakAnaDal, template.MufredatAdi }).IsUnique().HasFilter("[MufredatAdi] IS NOT NULL");
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(template => template.OlusturanKullaniciId);
        });

        builder.Entity<Ders>(entity =>
        {
            entity.ToTable("Dersler", table =>
            {
                table.HasCheckConstraint("CK_Ders_Akts", "[Akts] >= 0");
                table.HasCheckConstraint("CK_Ders_Tur", "[Tur] IN (1, 2)");
                table.HasCheckConstraint("CK_Ders_Kaynak", "([Tur] = 1 AND [KaynakAnaDal] IS NOT NULL AND LEN(LTRIM(RTRIM([KaynakAnaDal]))) > 0) OR ([Tur] = 2 AND [KaynakAnaDal] IS NULL)");
            });
            entity.Property(course => course.HedefBolum).HasMaxLength(128).IsRequired();
            entity.Property(course => course.KaynakAnaDal).HasMaxLength(128);
            entity.Property(course => course.Kod).HasMaxLength(50).IsRequired();
            entity.Property(course => course.Ad).HasMaxLength(250).IsRequired();
            entity.Property(course => course.Akts).HasPrecision(6, 2);
            entity.HasIndex(course => new { course.Tur, course.HedefBolum, course.KaynakAnaDal, course.Kod }).IsUnique().HasFilter(null);
        });

        builder.Entity<SablonDersi>(entity =>
        {
            entity.ToTable("SablonDersleri", table =>
            {
                table.HasCheckConstraint("CK_SablonDersi_Yariyil", "[MufredatYariyili] BETWEEN 1 AND 8");
                table.HasCheckConstraint("CK_SablonDersi_Tur", "[Tur] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_SablonDersi_Akts", "[Akts] IS NULL OR [Akts] >= 0");
                table.HasCheckConstraint("CK_SablonDersi_Kredi", "[Kredi] IS NULL OR [Kredi] >= 0");
            });
            entity.HasKey(item => new { item.SablonId, item.DersId });
            entity.Property(item => item.Kod).HasMaxLength(50);
            entity.Property(item => item.Ad).HasMaxLength(250);
            entity.Property(item => item.Akts).HasPrecision(6, 2);
            entity.Property(item => item.Kredi).HasPrecision(6, 2);
            entity.HasIndex(item => new { item.SablonId, item.Kod }).IsUnique().HasFilter("[EtkinMi] = 1 AND [Kod] IS NOT NULL");
            entity.HasOne<DersSablonu>().WithMany().HasForeignKey(item => item.SablonId);
            entity.HasOne<Ders>().WithMany().HasForeignKey(item => item.DersId);
        });

        builder.Entity<SecmeliGerekliligi>(entity =>
        {
            entity.ToTable("SecmeliGereklilikleri", table =>
            {
                table.HasCheckConstraint("CK_Secmeli_Sayi", "[GerekenSayi] >= 0 AND [MufredatYariyili] >= 1");
                table.HasCheckConstraint("CK_Secmeli_Tur", "[Tur] IN (1, 2)");
            });
            entity.HasIndex(item => new { item.SablonId, item.MufredatYariyili, item.Tur }).IsUnique();
            entity.HasOne<DersSablonu>().WithMany().HasForeignKey(item => item.SablonId);
        });

        builder.Entity<SablonDonemDersi>(entity =>
        {
            entity.ToTable("SablonDonemDersleri");
            entity.HasKey(item => new { item.SablonId, item.DonemId, item.DersId });
            entity.HasOne<SablonDersi>().WithMany().HasForeignKey(item => new { item.SablonId, item.DersId });
            entity.HasOne<Donem>().WithMany().HasForeignKey(item => item.DonemId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(item => item.DegistirenKullaniciId);
            entity.Property(item => item.OgretimElemani).HasMaxLength(200);
            entity.Property(item => item.RowVersion).IsRowVersion();
        });

        builder.Entity<DersSecimi>(entity =>
        {
            entity.ToTable("DersSecimleri", table => table.HasCheckConstraint("CK_DersSecimi_Onay", "([Durum] = 1 AND [OnaylayanKullaniciId] IS NULL AND [OnayAni] IS NULL AND [KararGerekcesi] IS NULL) OR ([Durum] = 2 AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL) OR ([Durum] IN (3,4) AND [OnaylayanKullaniciId] IS NOT NULL AND [OnayAni] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)"));
            entity.Property(list => list.KararGerekcesi).HasMaxLength(2000);
            entity.HasIndex(list => new { list.ProgramId, list.DonemId }).IsUnique();
            entity.HasAlternateKey(list => new { list.Id, list.ProgramId, list.DonemId, list.SablonId });
            entity.HasAlternateKey(list => new { list.Id, list.ProgramId });
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(list => list.ProgramId);
            entity.HasOne<Donem>().WithMany().HasForeignKey(list => list.DonemId);
            entity.HasOne<DersSablonu>().WithMany().HasForeignKey(list => list.SablonId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(list => list.GonderenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(list => list.OnaylayanKullaniciId);
            entity.Property(list => list.RowVersion).IsRowVersion();
        });

        builder.Entity<DersSecimKalemi>(entity =>
        {
            entity.ToTable("DersSecimKalemleri", table =>
            {
                table.HasCheckConstraint("CK_DersKalemi_Akts", "[AktsKopyasi] >= 0");
                table.HasCheckConstraint("CK_DersKalemi_Kredi", "[KrediKopyasi] IS NULL OR [KrediKopyasi] >= 0");
                table.HasCheckConstraint("CK_DersKalemi_Tur", "[TurKopyasi] IS NULL OR [TurKopyasi] IN (1,2,3)");
                table.HasCheckConstraint("CK_DersKalemi_Yariyil", "[MufredatYariyiliKopyasi] IS NULL OR [MufredatYariyiliKopyasi] BETWEEN 1 AND 8");
                table.HasCheckConstraint("CK_DersKalemi_Iptal", "([IptalAni] IS NULL AND [IptalEdenKullaniciId] IS NULL AND [IptalGerekcesi] IS NULL) OR ([IptalAni] IS NOT NULL AND [IptalEdenKullaniciId] IS NOT NULL AND [IptalGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([IptalGerekcesi]))) > 0)");
            });
            entity.HasIndex(item => new { item.ListeId, item.DersId }).IsUnique().HasFilter("[IptalAni] IS NULL");
            entity.HasOne<DersSecimi>().WithMany().HasForeignKey(item => new { item.ListeId, item.ProgramId, item.DonemId, item.SablonId })
                .HasPrincipalKey(list => new { list.Id, list.ProgramId, list.DonemId, list.SablonId });
            entity.HasOne<SablonDersi>().WithMany().HasForeignKey(item => new { item.SablonId, item.DersId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(item => item.EkleyenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(item => item.DuzeltenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(item => item.IptalEdenKullaniciId);
            entity.Property(item => item.DuzeltmeGerekcesi).HasMaxLength(2000);
            entity.Property(item => item.IptalGerekcesi).HasMaxLength(2000);
            entity.Property(item => item.DersKoduKopyasi).HasMaxLength(50).IsRequired();
            entity.Property(item => item.DersAdiKopyasi).HasMaxLength(250).IsRequired();
            entity.Property(item => item.AktsKopyasi).HasPrecision(6, 2);
            entity.Property(item => item.KrediKopyasi).HasPrecision(6, 2);
            entity.Property(item => item.OgretimElemaniKopyasi).HasMaxLength(200);
        });
    }
}
