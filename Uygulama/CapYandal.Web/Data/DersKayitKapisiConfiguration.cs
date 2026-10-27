using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;
internal static class DersKayitKapisiConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DersKayitKapisi>(e =>
        {
            e.ToTable("DersKayitKapilari", table =>
            {
                table.HasCheckConstraint("CK_KayitKapisi_Acilis", "[AcikMi] = 0 OR [AcilisAniUtc] IS NULL");
                table.HasCheckConstraint("CK_KayitKapisi_Sira", "[AcilisAniUtc] IS NULL OR [KapanisAniUtc] IS NULL OR [KapanisAniUtc] > [AcilisAniUtc]");
                table.HasCheckConstraint("CK_KayitKapisi_Planlayan", "([AcilisAniUtc] IS NULL AND [KapanisAniUtc] IS NULL) OR [PlanlayanKullaniciId] IS NOT NULL");
            });
            e.HasKey(g => g.DonemId);
            e.Property(g => g.DonemId).ValueGeneratedNever();
            e.Property(g => g.AcikMi).HasDefaultValue(false);
            e.Property(g => g.RowVersion).IsRowVersion();
            e.HasOne<Donem>().WithOne().HasForeignKey<DersKayitKapisi>(g => g.DonemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(g => g.PlanlayanKullaniciId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
