using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

internal static class GanoConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<AkademikKosulSurumu>(entity =>
        {
            entity.ToTable("AkademikKosulSurumleri", table =>
            {
                table.HasCheckConstraint("CK_AkademikKosulSurumu_Deger", "[ProgramTuru] IN (1, 2) AND [SurumNo] >= 1");
                table.HasCheckConstraint("CK_AkademikKosulSurumu_SayisalJson", "ISJSON([SayisalKurallarJson]) = 1 AND LEFT(LTRIM([SayisalKurallarJson]), 1) = N'['");
                table.HasTrigger("TR_AkademikKosulSurumleri_Degismez");
                table.UseSqlOutputClause(false);
            });
            entity.HasIndex(v => new { v.ProgramTuru, v.SurumNo }).IsUnique();
            entity.Property(v => v.SayisalKurallarJson).HasMaxLength(16000).HasDefaultValue("[]").IsRequired();
            entity.HasAlternateKey(v => new { v.Id, v.ProgramTuru });
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(v => v.OlusturanKullaniciId);
        });
        builder.Entity<GanoKontrolKaydi>(entity =>
        {
            entity.ToTable("GanoKontrolKayitlari", table =>
            {
                table.HasCheckConstraint("CK_Gano_Deger", "[Gano] BETWEEN 0 AND 4");
                table.HasCheckConstraint("CK_Gano_Kapsam", "[Kapsam] IN (1, 2, 3)");
            });
            entity.HasIndex(record => new { record.ProgramId, record.DonemId, record.Kapsam }).IsUnique();
            entity.Property(record => record.Gano).HasPrecision(3, 2);
            entity.Property(record => record.RowVersion).IsRowVersion();
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(record => record.ProgramId);
            entity.HasOne<Donem>().WithMany().HasForeignKey(record => record.DonemId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(record => record.GirenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(record => record.DuzeltenKullaniciId);
        });

        builder.Entity<KosulKumesi>(entity =>
        {
            entity.ToTable("KosulKumeleri", table =>
            {
                table.HasCheckConstraint("CK_KosulKumesi_Tarih", "[YururlukBitisi] IS NULL OR [YururlukBaslangici] <= [YururlukBitisi]");
                table.HasCheckConstraint("CK_KosulKumesi_Kapsam", "[ProgramTuru] IN (1, 2) AND [Kapsam] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_KosulKumesi_SurumKapsam", "[AkademikKosulSurumuId] IS NULL OR ([ProgramTuru] = 1 AND [Kapsam] IN (1, 2)) OR ([ProgramTuru] = 2 AND [Kapsam] IN (1, 3))");
                table.HasTrigger("TR_KosulKumeleri_SurumKoruma");
                table.UseSqlOutputClause(false);
            });
            // Aralık çakışması ayrıca aynı kapsamda Serializable işlemle korunacak.
            entity.HasIndex(set => new { set.ProgramTuru, set.Kapsam, set.YururlukBaslangici, set.YururlukBitisi });
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(set => set.OlusturanKullaniciId);
            entity.HasIndex(set => new { set.AkademikKosulSurumuId, set.Kapsam }).IsUnique()
                .HasFilter("[AkademikKosulSurumuId] IS NOT NULL");
            entity.HasOne<AkademikKosulSurumu>().WithMany().HasForeignKey(set => new { set.AkademikKosulSurumuId, set.ProgramTuru })
                .HasPrincipalKey(v => new { v.Id, v.ProgramTuru });
        });

        builder.Entity<KosulDegeri>(entity =>
        {
            entity.ToTable("KosulDegerleri", table =>
            {
                table.HasCheckConstraint("CK_Kosul_Esikler", "([NormalEsik] IS NULL OR [NormalEsik] BETWEEN 0 AND 4) AND ([AltEsik] IS NULL OR [AltEsik] BETWEEN 0 AND 4) AND ([AltEsik] IS NULL OR ([NormalEsik] IS NOT NULL AND [AltEsik] <= [NormalEsik]))");
                table.HasCheckConstraint("CK_Kosul_Izin", "([IzinSayisi] IS NULL OR [IzinSayisi] >= 0) AND ([ArdisikDonemSayisi] IS NULL OR [ArdisikDonemSayisi] >= 1) AND ([IzinTuru] IS NULL OR [IzinTuru] IN (1, 2))");
                table.HasCheckConstraint("CK_Kosul_Kullanim", "[DegerlendirmeTuru] IN (1, 2)");
                table.HasCheckConstraint("CK_Kosul_Tanim", "([KuralKimligi] IS NULL AND [Amac] IS NULL AND [KontrolTuru] IS NULL AND [SiraNo] IS NULL AND [Aciklama] IS NULL) OR ([KuralKimligi] IS NOT NULL AND [KuralKimligi] <> '00000000-0000-0000-0000-000000000000' AND [Amac] IS NOT NULL AND [Amac] IN (1, 2) AND [KontrolTuru] IS NOT NULL AND [KontrolTuru] IN (1, 2, 3) AND [SiraNo] IS NOT NULL AND [SiraNo] >= 1 AND [Aciklama] IS NOT NULL AND LEN(LTRIM(RTRIM([Aciklama]))) > 0 AND [KuralKodu] = REPLACE(CONVERT(nvarchar(36), [KuralKimligi]), '-', ''))");
                table.HasCheckConstraint("CK_Kosul_KontrolTuru", "[KontrolTuru] IS NULL OR ([NormalEsik] IS NOT NULL AND [ReferansDegeri] IS NULL AND [DegerlendirmeTuru] = 1 AND (([KontrolTuru] = 1 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NULL AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 2 AND [IzinSayisi] IS NOT NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 1 AND [ArdisikDonemSayisi] IS NULL) OR ([KontrolTuru] = 3 AND [AltEsik] IS NULL AND [IzinSayisi] IS NULL AND [IzinTuru] IS NOT NULL AND [IzinTuru] = 2 AND [ArdisikDonemSayisi] IS NOT NULL)))");
                table.HasTrigger("TR_KosulDegerleri_SurumKoruma");
                table.UseSqlOutputClause(false);
            });
            entity.Property(rule => rule.KuralKodu).HasMaxLength(80).IsRequired();
            entity.Property(rule => rule.NormalEsik).HasPrecision(3, 2);
            entity.Property(rule => rule.AltEsik).HasPrecision(3, 2);
            entity.Property(rule => rule.ReferansDegeri).HasPrecision(12, 2);
            entity.Property(rule => rule.Aciklama).HasMaxLength(2000);
            entity.HasIndex(rule => new { rule.KosulKumesiId, rule.KuralKimligi }).IsUnique().HasFilter("[KuralKimligi] IS NOT NULL");
            entity.HasIndex(rule => new { rule.KosulKumesiId, rule.SiraNo }).IsUnique().HasFilter("[SiraNo] IS NOT NULL");
            entity.HasAlternateKey(rule => new { rule.KosulKumesiId, rule.KuralKodu });
            entity.HasOne<KosulKumesi>().WithMany().HasForeignKey(rule => rule.KosulKumesiId);
        });

        builder.Entity<GanoKontrolSonucu>(entity =>
        {
            entity.ToTable("GanoKontrolSonuclari", table =>
            {
                table.HasCheckConstraint("CK_GanoSonuc_Renk", "([Renk] IS NOT NULL AND (([SonucKodu] = 1 AND [Renk] = 1) OR ([SonucKodu] = 2 AND [Renk] = 2) OR ([SonucKodu] = 3 AND [Renk] = 3))) OR ([SonucKodu] IN (4, 5) AND [Renk] IS NULL)");
                table.HasCheckConstraint("CK_GanoSonuc_Izin", "([KullanilanIzinSayisi] IS NULL OR [KullanilanIzinSayisi] >= 0) AND ([KullanilanDusukDonemSayisi] IS NULL OR [KullanilanDusukDonemSayisi] >= 0)");
                table.HasCheckConstraint("CK_GanoSonuc_Revizyon", "([RevizyonNo] IS NULL AND [GanoKopyasi] IS NULL) OR ([RevizyonNo] IS NOT NULL AND [RevizyonNo] >= 1 AND [GanoKopyasi] IS NOT NULL AND [GanoKopyasi] BETWEEN 0 AND 4)");
            });
            entity.Property(result => result.KuralKodu).HasMaxLength(80).IsRequired();
            entity.Property(result => result.KullanilanNormalEsik).HasPrecision(3, 2);
            entity.Property(result => result.KullanilanAltEsik).HasPrecision(3, 2);
            entity.Property(result => result.GanoKopyasi).HasPrecision(3, 2);
            // SQL Server, mevcuttaki tek NULL satırının da benzersiz olmasını sağlamalı
            entity.HasIndex(result => new { result.GanoKaydiId, result.KuralKodu, result.RevizyonNo }).IsUnique().HasFilter(null);
            entity.HasOne<GanoKontrolKaydi>().WithMany().HasForeignKey(result => result.GanoKaydiId);
            entity.HasOne<KosulDegeri>().WithMany().HasForeignKey(result => new { result.KosulKumesiId, result.KuralKodu })
                .HasPrincipalKey(rule => new { rule.KosulKumesiId, rule.KuralKodu });
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(result => result.KontrolEdenKullaniciId);
        });
    }
}
