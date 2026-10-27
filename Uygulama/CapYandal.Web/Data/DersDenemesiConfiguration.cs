using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapYandal.Web.Data;

// Ders notu ve geçmiş ilişkileri.
internal sealed class DersDenemesiConfiguration : IEntityTypeConfiguration<DersDenemesi>
{
    public void Configure(EntityTypeBuilder<DersDenemesi> entity)
    {
        entity.ToTable("DersDenemeleri", t => t.HasCheckConstraint("CK_DersDenemesi_Kaynak", "[Kaynak] IN (1,2,3)"));
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.ProgramId, x.DonemId });
        entity.HasIndex(x => x.DersSecimKalemiId).IsUnique();
        entity.HasOne<DersSecimKalemi>().WithMany()
            .HasForeignKey(x => new { x.DersSecimKalemiId, x.ProgramId, x.DonemId })
            .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.DonemId }).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.OlusturanKullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DersDenemeSurumuConfiguration : IEntityTypeConfiguration<DersDenemeSurumu>
{
    public void Configure(EntityTypeBuilder<DersDenemeSurumu> entity)
    {
        entity.ToTable("DersDenemeSurumleri", t =>
        {
            t.HasCheckConstraint("CK_DersDenemeSurumu_Durum", "[SurumNo] > 0 AND [Durum] IN (1,2,3,4,5,6) AND [OnayDurumu] IN (1,2,3,4)");
            t.HasCheckConstraint("CK_DersDenemeSurumu_GecilenDonem", "([Durum] = 3 AND [GecilenDonemId] IS NOT NULL) OR ([Durum] <> 3 AND [GecilenDonemId] IS NULL)");
            t.HasCheckConstraint("CK_DersDenemeSurumu_Gerekce", "[OnayDurumu] NOT IN (3,4) OR ([Gerekce] IS NOT NULL AND LEN(LTRIM(RTRIM([Gerekce]))) > 0)");
            t.HasCheckConstraint("CK_DersDenemeSurumu_SayisalNot", "[SayisalNot] IS NULL OR [SayisalNot] BETWEEN 0 AND 100");
        });
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => new { x.DenemeId, x.SurumNo }).IsUnique();
        entity.Property(x => x.Not).HasMaxLength(32);
        entity.Property(x => x.SayisalNot).HasPrecision(5, 2);
        entity.Property(x => x.Gerekce).HasMaxLength(2000);
        entity.HasOne<DersDenemesi>().WithMany().HasForeignKey(x => x.DenemeId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Donem>().WithMany().HasForeignKey(x => x.GecilenDonemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.YapanKullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}
