using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

internal static class KullaniciVeProgramConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<UygulamaKullanicisi>(entity =>
        {
            entity.ToTable("Kullanicilar", table =>
            {
                table.HasCheckConstraint("CK_Kullanici_OgrenciNo", "[OgrenciNo] IS NULL OR (DATALENGTH([OgrenciNo]) = 18 AND [OgrenciNo] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9]%' AND [UserName] IS NOT NULL AND DATALENGTH([UserName]) = 18 AND [UserName] COLLATE Latin1_General_100_BIN2 = [OgrenciNo] COLLATE Latin1_General_100_BIN2)");
                table.HasCheckConstraint("CK_Kullanici_OgrenciGeciciParolaYok", "[OgrenciNo] IS NULL OR ([ParolaDegisikligiZorunlu] = 0 AND [GeciciParolaBitisAni] IS NULL)");
                table.HasCheckConstraint("CK_Kullanici_KisiselEpostaOnayi", "[KisiselEpostaOnayliMi] = 0 OR [KisiselEposta] IS NOT NULL");
            });
            entity.Property(user => user.OgrenciNo).HasMaxLength(9);
            entity.HasIndex(user => user.OgrenciNo).IsUnique().HasFilter("[OgrenciNo] IS NOT NULL");
            entity.Property(user => user.Ad).HasMaxLength(100);
            entity.Property(user => user.Soyad).HasMaxLength(100);
            entity.Property(user => user.Email).HasColumnName("KurumsalEposta");
            entity.Property(user => user.EmailConfirmed).HasColumnName("KurumsalEpostaOnayliMi");
            entity.Property(user => user.KisiselEposta).HasMaxLength(256);
            entity.Property(user => user.BekleyenKisiselEposta).HasMaxLength(256);
        });

        builder.Entity<OgrenciProgrami>(entity =>
        {
            entity.ToTable("OgrenciProgramlari", table =>
            {
                table.HasCheckConstraint("CK_Program_Tur", "[Tur] IN (1, 2)");
                table.HasCheckConstraint("CK_Program_Hedef", "[HedefBolum] = N'Bilgisayar Mühendisliği'");
                table.HasCheckConstraint("CK_Program_KabulDonemi", "([KabulAkademikYilBaslangici] IS NULL AND [KabulYariyili] IS NULL) OR ([KabulAkademikYilBaslangici] IS NOT NULL AND [KabulYariyili] IS NOT NULL AND [KabulAkademikYilBaslangici] BETWEEN 1 AND 9998 AND [KabulYariyili] IN (1,2))");
                table.HasCheckConstraint("CK_Program_Karar", "([SonKarar] IS NULL AND [KararGerekcesi] IS NULL AND [KararVerenKullaniciId] IS NULL AND [KararAni] IS NULL) OR ([SonKarar] IS NOT NULL AND [SonKarar] IN (1,2,3) AND [AktifMi] = 0 AND [KararGerekcesi] IS NOT NULL AND LEN(LTRIM(RTRIM([KararGerekcesi]))) > 0 AND [KararVerenKullaniciId] IS NOT NULL AND [KararAni] IS NOT NULL AND (([SonKarar] = 1 AND [Tur] = 1) OR ([SonKarar] = 2 AND [Tur] = 2) OR [SonKarar] = 3))");
            });
            entity.Property(program => program.HedefBolum).HasMaxLength(128).IsRequired();
            entity.Property(program => program.KaynakAnaDal).HasMaxLength(128).IsRequired();
            entity.Property(program => program.KararGerekcesi).HasMaxLength(2000);
            entity.Property(program => program.RowVersion).IsRowVersion();
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(program => program.KararVerenKullaniciId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(program => new { program.OgrenciId, program.Tur }).IsUnique().HasFilter("[AktifMi] = 1");
            entity.HasIndex(program => new { program.OgrenciId, program.HedefBolum }).IsUnique().HasFilter("[AktifMi] = 1");
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(program => program.OgrenciId);
            entity.HasOne<DersSablonu>().WithMany().HasForeignKey(program => program.MufredatSablonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Donem>(entity =>
        {
            entity.ToTable("Donemler", table =>
            {
                table.HasCheckConstraint("CK_Donem_Tarih", "([BaslangicTarihi] IS NULL AND [BitisTarihi] IS NULL) OR ([BaslangicTarihi] IS NOT NULL AND [BitisTarihi] IS NOT NULL AND [BaslangicTarihi] <= [BitisTarihi])");
                table.HasCheckConstraint("CK_Donem_Yariyil", "[Yariyil] IN (1, 2)");
                table.HasCheckConstraint("CK_Donem_Yil", "[AkademikYilBaslangici] BETWEEN 1 AND 9998");
            });
            entity.Property(period => period.Ad).HasMaxLength(80).IsRequired();
            entity.Property(period => period.Kod).HasMaxLength(40).IsRequired();
            entity.HasIndex(period => new { period.AkademikYilBaslangici, period.Yariyil }).IsUnique();
            entity.HasIndex(period => period.IslemDonemiMi).IsUnique().HasFilter("[IslemDonemiMi] = 1");
            entity.Property(period => period.RowVersion).IsRowVersion();
        });

    }
}
