using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

internal static class EvrakVeIletisimConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EvrakGonderimi>(entity =>
        {
            entity.ToTable("EvrakGonderimleri");
            entity.HasAlternateKey(submission => new { submission.Id, submission.ProgramId });
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(submission => submission.ProgramId);
            entity.HasOne<Donem>().WithMany().HasForeignKey(submission => submission.DonemId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(submission => submission.GonderenKullaniciId);
        });

        builder.Entity<GenelBelgeTanimi>(entity =>
        {
            entity.ToTable("GenelBelgeTanimlari", table => table.HasCheckConstraint("CK_GenelBelge_Kategori",
                "[Tur] IN (N'Akademik takvim', N'Protokoller', N'Ders kayıt rehberi', N'Kurallar ve yönergeler', N'Ders kayıt formları')"));
            entity.Property(document => document.Tur).HasMaxLength(80).IsRequired();
            entity.HasAlternateKey(document => new { document.Id, document.Tur });
            entity.Property(document => document.RowVersion).IsRowVersion();
        });

        builder.Entity<Belge>(entity =>
        {
            entity.ToTable("Belgeler", table =>
            {
                table.HasCheckConstraint("CK_Belge_Baglam", "([Kapsam] = 1 AND [ProgramId] IS NULL AND [DersSecimiId] IS NULL AND [EvrakGonderimiId] IS NULL) OR ([Kapsam] = 2 AND [ProgramId] IS NOT NULL AND (([DersSecimiId] IS NOT NULL AND [EvrakGonderimiId] IS NULL) OR ([DersSecimiId] IS NULL AND [EvrakGonderimiId] IS NOT NULL)))");
                table.HasCheckConstraint("CK_Belge_Kaynak", "([ResmiBaglanti] IS NOT NULL AND [Kapsam] = 1 AND [ResmiBaglanti] LIKE N'https://%' AND [DepolamaAnahtari] IS NULL AND [DosyaAdi] IS NULL AND [IcerikTuru] IS NULL AND [BoyutBayt] IS NULL) OR ([ResmiBaglanti] IS NULL AND [DepolamaAnahtari] IS NOT NULL AND [DosyaAdi] IS NOT NULL AND [IcerikTuru] IS NOT NULL AND [BoyutBayt] IS NOT NULL AND [BoyutBayt] >= 0)");
                table.HasCheckConstraint("CK_Belge_IcerikDurumu", "([IcerikDurumu] IN (1, 2) AND [IcerikKaldirilmaAni] IS NULL AND [KaldiranKullaniciId] IS NULL) OR ([IcerikDurumu] = 3 AND [IcerikKaldirilmaAni] IS NOT NULL AND [KaldiranKullaniciId] IS NOT NULL)");
                table.HasCheckConstraint("CK_Belge_GenelSurum", "([Kapsam] = 1 AND (([GenelBelgeId] IS NULL AND [SurumNo] IS NULL) OR ([GenelBelgeId] IS NOT NULL AND [SurumNo] IS NOT NULL AND [SurumNo] > 0))) OR ([Kapsam] = 2 AND [GenelBelgeId] IS NULL AND [SurumNo] IS NULL)");
                table.HasCheckConstraint("CK_Belge_GenelDosyaOzet", "[GenelBelgeId] IS NULL OR [ResmiBaglanti] IS NOT NULL OR ([Sha256] IS NOT NULL AND DATALENGTH([Sha256]) = 128 AND [Sha256] COLLATE Latin1_General_BIN2 NOT LIKE N'%[^0-9A-F]%')");
                table.HasCheckConstraint("CK_Belge_GenelDosyaBoyut", "[GenelBelgeId] IS NULL OR [ResmiBaglanti] IS NOT NULL OR ([BoyutBayt] IS NOT NULL AND [BoyutBayt] > 0 AND [BoyutBayt] <= 10485760)");
                table.HasCheckConstraint("CK_Belge_GenelArsiv", "[GenelArsivdeMi] = 0 OR ([Kapsam] = 1 AND [GenelBelgeId] IS NOT NULL)");
            });
            entity.Property(document => document.Tur).HasMaxLength(80).IsRequired();
            entity.Property(document => document.DosyaAdi).HasMaxLength(255);
            entity.Property(document => document.IcerikTuru).HasMaxLength(100);
            entity.Property(document => document.DepolamaAnahtari).HasMaxLength(128);
            entity.Property(document => document.ResmiBaglanti).HasMaxLength(2048);
            entity.Property(document => document.RowVersion).IsRowVersion();
            entity.Property(document => document.Sha256).HasMaxLength(64);
            entity.HasIndex(document => new { document.GenelBelgeId, document.SurumNo }).IsUnique()
                .HasFilter("[Kapsam] = 1 AND [GenelBelgeId] IS NOT NULL AND [SurumNo] IS NOT NULL");
            entity.HasOne<GenelBelgeTanimi>().WithMany().HasForeignKey(document => new { document.GenelBelgeId, document.Tur })
                .HasPrincipalKey(document => new { document.Id, document.Tur }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(document => document.ProgramId);
            entity.HasOne<DersSecimi>().WithMany().HasForeignKey(document => new { document.DersSecimiId, document.ProgramId })
                .HasPrincipalKey(list => new { list.Id, list.ProgramId });
            entity.HasOne<EvrakGonderimi>().WithMany().HasForeignKey(document => new { document.EvrakGonderimiId, document.ProgramId })
                .HasPrincipalKey(submission => new { submission.Id, submission.ProgramId });
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(document => document.YukleyenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(document => document.KaldiranKullaniciId);
        });

        builder.Entity<SohbetMesaji>(entity =>
        {
            entity.ToTable("SohbetMesajlari");
            entity.Property(message => message.Metin).IsRequired();
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(message => message.OgrenciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(message => message.GonderenKullaniciId);
            entity.HasIndex(message => new { message.OgrenciId, message.GonderimAni });
        });

        builder.Entity<DegisiklikGecmisi>(entity =>
        {
            entity.ToTable("DegisiklikGecmisi", table =>
            {
                table.HasTrigger("TR_DegisiklikGecmisi_Degismez");
                table.UseSqlOutputClause(false);
                table.HasCheckConstraint("CK_Denetim_Aktor", "[YapanKullaniciId] IS NOT NULL OR ([VarlikTuru] = N'GuvenlikTalebi' AND [Islem] IN (N'GirisTalebi', N'ParolaSifirlamaTalebi'))");
            });
            entity.Property(entry => entry.VarlikTuru).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.VarlikAnahtari).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.Islem).HasMaxLength(100).IsRequired();
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(entry => entry.YapanKullaniciId);
            entity.HasIndex(entry => new { entry.VarlikTuru, entry.VarlikAnahtari, entry.IslemAni });
        });

        builder.Entity<EpostaGonderimKaydi>(entity =>
        {
            entity.ToTable("EpostaGonderimKayitlari", table => table.HasCheckConstraint("CK_Eposta_Durum", "[Durum] IN (1, 2)"));
            entity.Property(mail => mail.Saglayici).HasMaxLength(40).IsRequired();
            entity.Property(mail => mail.Alici).HasMaxLength(256).IsRequired();
            entity.Property(mail => mail.Konu).HasMaxLength(250).IsRequired();
            entity.Property(mail => mail.HataOzeti).HasMaxLength(2000);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(mail => mail.KullaniciId);
        });
    }
}
