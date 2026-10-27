using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapYandal.Web.Data;

public sealed class DersDuzeltmeTalebiConfiguration : IEntityTypeConfiguration<DersDuzeltmeTalebi>
{
    public void Configure(EntityTypeBuilder<DersDuzeltmeTalebi> e)
    {
        e.ToTable("DersDuzeltmeTalepleri", t => {
            t.HasCheckConstraint("CK_DersDuzeltme_Gerekce", "LEN(LTRIM(RTRIM([Gerekce])))>0");
            t.HasCheckConstraint("CK_DersDuzeltme_Kapanis", "([KapatanKullaniciId] IS NULL AND [KapatilmaAni] IS NULL) OR ([KapatanKullaniciId] IS NOT NULL AND [KapatilmaAni] IS NOT NULL AND [KapatilmaAni]>=[IstekAni])");
            t.HasTrigger("TR_DersDuzeltmeTalepleri_Koru"); t.UseSqlOutputClause(false);
        });
        e.HasKey(x => x.Id); e.Property(x => x.Gerekce).HasMaxLength(2000).IsRequired(); e.Property(x => x.RowVersion).IsRowVersion();
        e.HasIndex(x => new { x.ListeId, x.KalemId }).IsUnique().HasFilter("[KapatilmaAni] IS NULL");
        e.HasOne<DersSecimKalemi>().WithMany().HasForeignKey(x => new { x.KalemId, x.ListeId }).HasPrincipalKey(x => new { x.Id, x.ListeId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.IsteyenKullaniciId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.KapatanKullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}
