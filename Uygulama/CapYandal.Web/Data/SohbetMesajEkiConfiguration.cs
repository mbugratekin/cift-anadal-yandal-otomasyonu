using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapYandal.Web.Data;


internal sealed class SohbetMesajEkiConfiguration : IEntityTypeConfiguration<SohbetMesajEki>
{
    public void Configure(EntityTypeBuilder<SohbetMesajEki> entity)
    {
        entity.ToTable("SohbetMesajEkleri", table => table.HasCheckConstraint("CK_SohbetMesajEki_Boyut", "[BoyutBayt] BETWEEN 1 AND 10485760"));
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.SohbetMesajiId).IsUnique();
        entity.Property(x => x.DosyaAdi).HasMaxLength(250).IsRequired();
        entity.Property(x => x.DepolamaAnahtari).HasMaxLength(40).IsRequired();
        entity.Property(x => x.IcerikTuru).HasMaxLength(120).IsRequired();
        entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        entity.HasOne<SohbetMesaji>().WithMany().HasForeignKey(x => x.SohbetMesajiId).OnDelete(DeleteBehavior.Restrict);
    }
}
