using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

// Belge arşivi: nullable eski alanlar korunur yeni sürümlerde kapsam ve dönem FK ile sabittir.
internal static class BelgeArsiviConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DersSecimi>().HasAlternateKey(x => new { x.Id, x.ProgramId, x.DonemId });
        builder.Entity<EvrakGonderimi>().HasAlternateKey(x => new { x.Id, x.ProgramId, x.DonemId });
        builder.Entity<KisiselBelgeSerisi>(entity =>
        {
            entity.ToTable("KisiselBelgeSerileri", t => t.HasCheckConstraint("CK_KisiselBelgeSerisi_Tur", "[Tur] IN (1,2,3,4,5)"));
            entity.HasAlternateKey(x => new { x.Id, x.ProgramId, x.DonemId, x.Tur });
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(x => x.ProgramId);
            entity.HasOne<Donem>().WithMany().HasForeignKey(x => x.DonemId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.OlusturanKullaniciId);
        });
        builder.Entity<EkBelgeIstegi>(entity =>
        {
            entity.ToTable("EkBelgeIstekleri", table =>
            {
                table.HasCheckConstraint("CK_EkBelgeIstegi_TurDurum", "[BelgeTuru] IN (1,2,3,4,5) AND [Durum] IN (1,2,3,4,5) AND LEN(LTRIM(RTRIM([IstekMetni]))) > 0");
                table.HasCheckConstraint("CK_EkBelgeIstegi_Karar", "([Durum] IN (1,2) AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([Durum] = 3 AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL) OR ([Durum] IN (4,5) AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)");
            });
            entity.HasAlternateKey(x => new { x.Id, x.ProgramId, x.DonemId, x.BelgeTuru });
            entity.Property(x => x.IstekMetni).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.KararGerekcesi).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(x => x.ProgramId);
            entity.HasOne<Donem>().WithMany().HasForeignKey(x => x.DonemId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.IsteyenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.KararVerenKullaniciId);
            entity.HasIndex(x => new { x.ProgramId, x.DonemId, x.Durum });
        });
        builder.Entity<Belge>(entity =>
        {
            entity.ToTable("Belgeler", table =>
            {
                table.HasCheckConstraint("CK_Belge_ArsivSerisi", "([KisiselBelgeSerisiId] IS NULL AND [KisiselSurumNo] IS NULL AND [KisiselTur] IS NULL AND [ArsivDonemId] IS NULL AND [EkBelgeIstegiId] IS NULL) OR ([Kapsam] = 2 AND [KisiselBelgeSerisiId] IS NOT NULL AND [KisiselSurumNo] IS NOT NULL AND [KisiselSurumNo] > 0 AND [KisiselTur] IS NOT NULL AND [KisiselTur] IN (1,2,3,4,5) AND [ArsivDonemId] IS NOT NULL AND [ProgramId] IS NOT NULL AND [OnayDurumu] IS NOT NULL)");
                table.HasCheckConstraint("CK_Belge_OnayKarari", "([OnayDurumu] IS NULL AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([Kapsam] = 2 AND [OnayDurumu] IS NOT NULL AND (([OnayDurumu] = 1 AND [KararAni] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararGerekcesi] IS NULL) OR ([OnayDurumu] = 2 AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL) OR ([OnayDurumu] IN (3,4) AND [KararAni] IS NOT NULL AND [KararVerenKullaniciId] IS NOT NULL AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0)))");
            });
            entity.Property(x => x.KararGerekcesi).HasMaxLength(2000);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.KararVerenKullaniciId);
            entity.HasIndex(x => new { x.KisiselBelgeSerisiId, x.KisiselSurumNo }).IsUnique().HasFilter("[KisiselBelgeSerisiId] IS NOT NULL");
            entity.HasOne<KisiselBelgeSerisi>().WithMany()
                .HasForeignKey(x => new { x.KisiselBelgeSerisiId, x.ProgramId, x.ArsivDonemId, x.KisiselTur })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.DonemId, x.Tur });
            entity.HasOne<EkBelgeIstegi>().WithMany()
                .HasForeignKey(x => new { x.EkBelgeIstegiId, x.ProgramId, x.ArsivDonemId, x.KisiselTur })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.DonemId, x.BelgeTuru });
            entity.HasOne<DersSecimi>().WithMany().HasForeignKey(x => new { x.DersSecimiId, x.ProgramId, x.ArsivDonemId })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.DonemId });
            entity.HasOne<EvrakGonderimi>().WithMany().HasForeignKey(x => new { x.EvrakGonderimiId, x.ProgramId, x.ArsivDonemId })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.DonemId });
        });
    }
}
