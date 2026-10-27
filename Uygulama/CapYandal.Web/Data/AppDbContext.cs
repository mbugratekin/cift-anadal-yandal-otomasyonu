using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<UygulamaKullanicisi, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<KullaniciFotografi> KullaniciFotograflari => Set<KullaniciFotografi>();
    public DbSet<PersonelGorusmeBilgisi> PersonelGorusmeBilgileri => Set<PersonelGorusmeBilgisi>();
    public DbSet<OgrenciProgrami> OgrenciProgramlari => Set<OgrenciProgrami>();
    public DbSet<ProgramTakipNotu> ProgramTakipNotlari => Set<ProgramTakipNotu>();
    public DbSet<Donem> Donemler => Set<Donem>();
    public DbSet<ProgramDonemIzni> ProgramDonemIzinleri => Set<ProgramDonemIzni>();
    public DbSet<DersKayitKapisi> DersKayitKapilari => Set<DersKayitKapisi>();
    public DbSet<DersSablonu> DersSablonlari => Set<DersSablonu>();
    public DbSet<Mufredat> Mufredatlar => Set<Mufredat>();
    public DbSet<KaynakAnaDal> KaynakAnaDallar => Set<KaynakAnaDal>();
    public DbSet<Ders> Dersler => Set<Ders>();
    public DbSet<SablonDersi> SablonDersleri => Set<SablonDersi>();
    public DbSet<SecmeliGerekliligi> SecmeliGereklilikleri => Set<SecmeliGerekliligi>();
    public DbSet<SablonDonemDersi> SablonDonemDersleri => Set<SablonDonemDersi>();
    public DbSet<DersSecimi> DersSecimleri => Set<DersSecimi>();
    public DbSet<DersListeSurumu> DersListeSurumleri => Set<DersListeSurumu>();
    public DbSet<DersSecimKalemi> DersSecimKalemleri => Set<DersSecimKalemi>();
    public DbSet<DersDuzeltmeTalebi> DersDuzeltmeTalepleri => Set<DersDuzeltmeTalebi>();
    public DbSet<DersDenemesi> DersDenemeleri => Set<DersDenemesi>();
    public DbSet<DersDenemeSurumu> DersDenemeSurumleri => Set<DersDenemeSurumu>();
    public DbSet<EvrakGonderimi> EvrakGonderimleri => Set<EvrakGonderimi>();
    public DbSet<Belge> Belgeler => Set<Belge>();
    public DbSet<KisiselBelgeSerisi> KisiselBelgeSerileri => Set<KisiselBelgeSerisi>();
    public DbSet<EkBelgeIstegi> EkBelgeIstekleri => Set<EkBelgeIstegi>();
    public DbSet<GenelBelgeTanimi> GenelBelgeTanimlari => Set<GenelBelgeTanimi>();
    public DbSet<GanoKontrolKaydi> GanoKontrolKayitlari => Set<GanoKontrolKaydi>();
    public DbSet<GanoKontrolSonucu> GanoKontrolSonuclari => Set<GanoKontrolSonucu>();
    public DbSet<KosulKumesi> KosulKumeleri => Set<KosulKumesi>();
    public DbSet<AkademikKosulSurumu> AkademikKosulSurumleri => Set<AkademikKosulSurumu>();
    public DbSet<KosulDegeri> KosulDegerleri => Set<KosulDegeri>();
    public DbSet<SohbetMesaji> SohbetMesajlari => Set<SohbetMesaji>();
    public DbSet<SohbetMesajEki> SohbetMesajEkleri => Set<SohbetMesajEki>();
    public DbSet<SohbetTemizlikDurumu> SohbetTemizlikDurumlari => Set<SohbetTemizlikDurumu>();
    public DbSet<DegisiklikGecmisi> DegisiklikGecmisi => Set<DegisiklikGecmisi>();
    public DbSet<EpostaGonderimKaydi> EpostaGonderimKayitlari => Set<EpostaGonderimKaydi>();

    // İşlem geçmişindeki kayıtlar değiştirilemez ve silinemez.
    private void DenetimGecmisiniKoru()
    {
        foreach (var entry in ChangeTracker.Entries<DersDuzeltmeTalebi>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                (entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(DersDuzeltmeTalebi.KapatilmaAni)) is not null ||
                 entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(DersDuzeltmeTalebi.KapatanKullaniciId) or nameof(DersDuzeltmeTalebi.KapatilmaAni)))))
                throw new InvalidOperationException("Ders düzeltme isteğinin geçmişi değiştirilemez.");
        }
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<ProgramDonemIzni>().Any(e => e.State == EntityState.Deleted ||
            e.State == EntityState.Modified && (e.Property(x => x.IptalAni).OriginalValue != null ||
            new[] { nameof(ProgramDonemIzni.ProgramId), nameof(ProgramDonemIzni.Baslangic), nameof(ProgramDonemIzni.Bitis),
                nameof(ProgramDonemIzni.Tur), nameof(ProgramDonemIzni.Gerekce), nameof(ProgramDonemIzni.BelgeId),
                nameof(ProgramDonemIzni.KaydedenId), nameof(ProgramDonemIzni.KayitAni) }.Any(p => e.Property(p).IsModified))))
            throw new InvalidOperationException("İzin geçmişi değiştirilemez; gerekçeli iptal ve yeni kayıt kullanın.");
        if (ChangeTracker.Entries<AkademikKosulSurumu>().Any(e => e.State == EntityState.Deleted ||
            e.State == EntityState.Modified && (e.Property(x => x.TamamlandiMi).OriginalValue || e.Property(x => x.SayisalKurallarJson).IsModified)))
            throw new InvalidOperationException("Kural sürümü değişmez; yeni sürüm kaydedilmelidir.");
        string[] noteIdentityFields = [nameof(ProgramTakipNotu.ProgramId), nameof(ProgramTakipNotu.OlusturanKullaniciId), nameof(ProgramTakipNotu.OlusturmaAni)];
        if (ChangeTracker.Entries<ProgramTakipNotu>().Any(e => e.State == EntityState.Deleted ||
            (e.State == EntityState.Modified && (e.Property(x => x.SilindiMi).OriginalValue || noteIdentityFields.Any(name => e.Property(name).IsModified)))))
            throw new InvalidOperationException("Takip notu kalıcıdır; fiziksel silme veya iptali kaldırma yapılamaz.");
        if (ChangeTracker.Entries<DersSecimi>().Any(e => e.State == EntityState.Modified && e.Property(x => x.GecmisAktarimiMi).IsModified))
            throw new InvalidOperationException("Ders listesinin aktarım kaynağı değiştirilemez.");
        if (ChangeTracker.Entries<DersListeSurumu>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Eski liste sürümü değiştirilemez veya silinemez.");
        if (ChangeTracker.Entries<SohbetMesajEki>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Kalıcı mesaj eki değiştirilemez veya silinemez.");
        string[] messageFields = [nameof(SohbetMesaji.OgrenciId), nameof(SohbetMesaji.GonderenKullaniciId),
            nameof(SohbetMesaji.Metin), nameof(SohbetMesaji.GonderimAni), nameof(SohbetMesaji.KaldirmaAni)];
        if (ChangeTracker.Entries<SohbetMesaji>().Any(e => e.State == EntityState.Deleted ||
            (e.State == EntityState.Modified && messageFields.Any(name => e.Property(name).IsModified))))
            throw new InvalidOperationException("Kalıcı mesaj silinemez veya metni değiştirilemez.");
        string[] documentFields = [nameof(Belge.Kapsam), nameof(Belge.ProgramId), nameof(Belge.GenelBelgeId),
            nameof(Belge.KisiselBelgeSerisiId), nameof(Belge.KisiselSurumNo), nameof(Belge.SurumNo),
            nameof(Belge.KisiselTur), nameof(Belge.ArsivDonemId), nameof(Belge.EkBelgeIstegiId),
            nameof(Belge.IcerikTuru), nameof(Belge.Sha256), nameof(Belge.DepolamaAnahtari),
            nameof(Belge.DosyaAdi), nameof(Belge.BoyutBayt), nameof(Belge.YukleyenKullaniciId), nameof(Belge.YuklemeAni)];
        if (ChangeTracker.Entries<Belge>().Any(e => e.State == EntityState.Deleted ||
            (e.State == EntityState.Modified && documentFields.Any(name => e.Property(name).IsModified))))
            throw new InvalidOperationException("Arşiv belgesi silinemez veya dosya kimliği değiştirilemez.");
        if (ChangeTracker.Entries<DegisiklikGecmisi>()
            .Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("İşlem geçmişi değiştirilemez veya silinemez.");
        if (ChangeTracker.Entries<DegisiklikGecmisi>().Any(x => x.State == EntityState.Added && x.Entity.YapanKullaniciId is null &&
            (x.Entity.VarlikTuru != "GuvenlikTalebi" || x.Entity.Islem is not ("GirisTalebi" or "ParolaSifirlamaTalebi"))))
            throw new InvalidOperationException("İş değişikliklerinde işlemi yapan kullanıcı zorunludur.");
        if (ChangeTracker.Entries<KisiselBelgeSerisi>().Any(e => e.State == EntityState.Modified &&
            e.Property(x => x.OgrenciKilidi).OriginalValue && !e.Property(x => x.OgrenciKilidi).CurrentValue))
            throw new InvalidOperationException("Onay sonrası öğrenci belge kilidi kaldırılamaz.");
        if (ChangeTracker.Entries<DersSecimi>().Any(e => e.State == EntityState.Modified &&
            e.Property(x => x.OgrenciKilidi).OriginalValue && !e.Property(x => x.OgrenciKilidi).CurrentValue))
            throw new InvalidOperationException("Onay sonrası öğrenci ders listesi kilidi kaldırılamaz.");
        if (ChangeTracker.Entries<DersDenemesi>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<DersDenemeSurumu>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Ders denemesi geçmişi değiştirilemez; yeni sürüm kaydedilmelidir.");
        string[] snapshotFields = [nameof(DersSecimKalemi.DersKoduKopyasi), nameof(DersSecimKalemi.DersAdiKopyasi),
            nameof(DersSecimKalemi.AktsKopyasi), nameof(DersSecimKalemi.KrediKopyasi), nameof(DersSecimKalemi.TurKopyasi),
            nameof(DersSecimKalemi.MufredatYariyiliKopyasi), nameof(DersSecimKalemi.OgretimElemaniKopyasi)];
        if (ChangeTracker.Entries<DersSecimKalemi>().Any(e => e.State == EntityState.Deleted ||
            (e.State == EntityState.Modified && snapshotFields.Any(name => e.Property(name).IsModified))))
            throw new InvalidOperationException("Gönderilmiş ders kopyası silinemez veya değiştirilemez; gerekçeli iptal kullanılmalıdır.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        DenetimGecmisiniKoru();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        DenetimGecmisiniKoru();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<KaynakAnaDal>(e =>
        {
            e.ToTable("KaynakAnaDallar", t => t.HasCheckConstraint("CK_KaynakAnaDal_Tur", "[Tur] IN (1,2)"));
            e.HasKey(x => x.Id); e.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.Tur, x.Ad }).IsUnique(); e.Property(x => x.RowVersion).IsRowVersion();
        });
        KullaniciVeProgramConfiguration.Configure(builder);
        builder.Entity<PersonelGorusmeBilgisi>(entity =>
        {
            entity.ToTable("PersonelGorusmeBilgileri", table => table.HasCheckConstraint("CK_Gorusme_Json", "ISJSON([AraliklarJson]) = 1"));
            entity.HasKey(x => x.KullaniciId);
            entity.Property(x => x.Blok).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Oda).HasMaxLength(80).IsRequired();
            entity.Property(x => x.AraliklarJson).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne<UygulamaKullanicisi>().WithOne().HasForeignKey<PersonelGorusmeBilgisi>(x => x.KullaniciId);
        });
        builder.Entity<KullaniciFotografi>(entity =>
        {
            entity.ToTable("KullaniciFotograflari", table =>
                table.HasCheckConstraint("CK_Fotograf_Boyut", "DATALENGTH([JpegIcerik]) BETWEEN 1 AND 1048576"));
            entity.HasKey(photo => photo.KullaniciId);
            entity.Property(photo => photo.JpegIcerik).IsRequired().HasMaxLength(1048576);
            entity.HasOne<UygulamaKullanicisi>().WithOne().HasForeignKey<KullaniciFotografi>(photo => photo.KullaniciId);
        });
        DersConfiguration.Configure(builder);
        builder.Entity<Mufredat>(e => {
            e.ToTable("Mufredatlar", t => t.HasCheckConstraint("CK_Mufredat_Baslangic", "[BaslangicYili] IS NULL OR [BaslangicYili] BETWEEN 2 AND 9998"));
            e.Property(x => x.Ad).HasMaxLength(100).IsRequired(); e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Ad).IsUnique(); e.HasIndex(x => x.BaslangicYili).IsUnique().HasFilter("[BaslangicYili] IS NOT NULL");
            e.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.OlusturanKullaniciId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DersSablonu>().HasOne<Mufredat>().WithMany().HasForeignKey(x => x.MufredatId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProgramDonemIzni>(entity =>
        {
            entity.ToTable("ProgramDonemIzinleri", t =>
            {
                t.HasTrigger("TR_ProgramDonemIzinleri_Koru");
                t.UseSqlOutputClause(false);
                t.HasCheckConstraint("CK_DonemIzni_Aralik", "[Baslangic] BETWEEN 2 AND 19997 AND [Bitis] BETWEEN [Baslangic] AND 19997");
                t.HasCheckConstraint("CK_DonemIzni_Tur", "[Tur] IN (N'Izin', N'Dondurma') AND LEN(LTRIM(RTRIM([Gerekce]))) > 0");
                t.HasCheckConstraint("CK_DonemIzni_Iptal", "([IptalAni] IS NULL AND [IptalEdenId] IS NULL AND [IptalGerekcesi] IS NULL) OR ([IptalAni] IS NOT NULL AND [IptalEdenId] IS NOT NULL AND LEN(LTRIM(RTRIM([IptalGerekcesi]))) > 0 AND [IptalGerekcesi] IS NOT NULL)");
            });
            entity.Property(x => x.Tur).HasMaxLength(16);
            entity.Property(x => x.Gerekce).HasMaxLength(2000);
            entity.Property(x => x.IptalGerekcesi).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.ProgramId, x.Baslangic, x.Bitis });
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(x => x.ProgramId);
            entity.HasOne<Belge>().WithMany().HasForeignKey(x => x.BelgeId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.KaydedenId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(x => x.IptalEdenId);
        });
        builder.ApplyConfiguration(new DersListeSurumuConfiguration());
        builder.ApplyConfiguration(new DersDuzeltmeTalebiConfiguration());
        builder.Entity<DersSecimKalemi>().HasAlternateKey(x => new { x.Id, x.ProgramId, x.DonemId });
        builder.ApplyConfiguration(new DersDenemesiConfiguration());
        builder.ApplyConfiguration(new DersDenemeSurumuConfiguration());
        DersKayitKapisiConfiguration.Configure(builder);
        GanoConfiguration.Configure(builder);
        EvrakVeIletisimConfiguration.Configure(builder);
        builder.ApplyConfiguration(new SohbetMesajEkiConfiguration());
        BelgeArsiviConfiguration.Configure(builder);
        builder.Entity<SohbetTemizlikDurumu>(entity =>
        {
            entity.ToTable("SohbetTemizlikDurumlari", table => table.HasCheckConstraint("CK_SohbetTemizlik_Tek", "[Id] = 1"));
            entity.Property(x => x.Id).ValueGeneratedNever();
        });

        builder.Entity<ProgramTakipNotu>(entity =>
        {
            entity.ToTable("ProgramTakipNotlari", table =>
            {
                table.HasCheckConstraint("CK_ProgramTakipNotu_Metin", "LEN(LTRIM(RTRIM([Metin]))) > 0");
                table.HasCheckConstraint("CK_ProgramTakipNotu_Silme", "([SilindiMi] = 0 AND [SilmeAni] IS NULL AND [SilenKullaniciId] IS NULL) OR ([SilindiMi] = 1 AND [SilmeAni] IS NOT NULL AND [SilenKullaniciId] IS NOT NULL)");
            });
            entity.Property(not => not.Metin).IsRequired().HasMaxLength(2000);
            entity.Property(not => not.RowVersion).IsRowVersion();
            entity.HasOne<OgrenciProgrami>().WithMany().HasForeignKey(not => not.ProgramId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(not => not.OlusturanKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(not => not.DuzeltenKullaniciId);
            entity.HasOne<UygulamaKullanicisi>().WithMany().HasForeignKey(not => not.SilenKullaniciId);
        });

        foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
