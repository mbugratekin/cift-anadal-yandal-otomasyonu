using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapYandal.Web.Data;

internal sealed class DersListeSurumuConfiguration : IEntityTypeConfiguration<DersListeSurumu>
{
    public void Configure(EntityTypeBuilder<DersListeSurumu> entity)
    {
        entity.ToTable("DersListeSurumleri", t => t.HasCheckConstraint("CK_DersListeSurumu", "[SurumNo] > 0 AND [Durum] IN (1,2,3,4) AND ISJSON([KopyaJson]) = 1"));
        entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ListeId, x.SurumNo }).IsUnique();
        entity.Property(x => x.Gerekce).HasMaxLength(2000); entity.Property(x => x.KopyaJson).IsRequired();
        entity.HasOne<DersSecimi>().WithMany().HasForeignKey(x => x.ListeId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.YapanKullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}
