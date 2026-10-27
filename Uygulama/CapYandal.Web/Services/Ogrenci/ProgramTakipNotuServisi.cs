using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Ogrenci;

public enum ProgramTakipNotuSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }

public sealed class ProgramTakipNotuServisi(UserManager<UygulamaKullanicisi> users,
    AppDbContext database, HesapErisimi access, ILogger<ProgramTakipNotuServisi> logger)
{
    public ProgramTakipNotuSatirModel? SonKayit { get; private set; }

    private static ProgramTakipNotuSatirModel Satir(ProgramTakipNotu not) =>
        new(not.Id, not.Metin, not.OlusturmaAni, Convert.ToBase64String(not.RowVersion));

    public async Task<ProgramTakipNotuSonucu> EkleAsync(Guid actorId, ProgramTakipNotuEkleModeli model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || model is null || model.ProgramId == Guid.Empty ||
            string.IsNullOrWhiteSpace(model.Metin) || model.Metin.Length > 2000 ||
            !Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true))
            return ProgramTakipNotuSonucu.Gecersiz;

        try
        {
            // Dış işlem kullanılırken aynı sürüm kontrolü korunur.
            if (database.Database.CurrentTransaction is { } callerTransaction &&
                callerTransaction.GetDbTransaction().IsolationLevel is not
                    (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
                return ProgramTakipNotuSonucu.Basarisiz;

            await using var ownedTx = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
            var tx = ownedTx ?? database.Database.CurrentTransaction!;
            var savepoint = Guid.NewGuid().ToString("N");
            if (ownedTx is null) await tx.CreateSavepointAsync(savepoint, cancellationToken);
            var completed = false;
            try
            {
                if (!await YetkiliMiAsync(actorId, cancellationToken)) return ProgramTakipNotuSonucu.IzinYok;

                var programGecerli = await database.OgrenciProgramlari.AsNoTracking().AnyAsync(p =>
                    p.Id == model.ProgramId && p.HedefBolum == "Bilgisayar Mühendisliği" &&
                    (p.Tur == OgrenciProgramTuru.Cap || p.Tur == OgrenciProgramTuru.YanDal), cancellationToken);
                if (!programGecerli) return ProgramTakipNotuSonucu.Bulunamadi;

                var now = DateTimeOffset.UtcNow;
                var not = new ProgramTakipNotu
                {
                    Id = Guid.NewGuid(), ProgramId = model.ProgramId, Metin = model.Metin,
                    OlusturanKullaniciId = actorId, OlusturmaAni = now
                };
                database.Set<ProgramTakipNotu>().Add(not);
                database.DegisiklikGecmisi.Add(new DegisiklikGecmisi
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(ProgramTakipNotu), VarlikAnahtari = not.Id.ToString(),
                    Islem = "ProgramTakipNotuEkle", YapanKullaniciId = actorId, IslemAni = now,
                    YeniDeger = JsonSerializer.Serialize(new { not.ProgramId, not.Metin, not.OlusturmaAni, not.OlusturanKullaniciId })
                });

                await database.SaveChangesAsync(cancellationToken);
                if (ownedTx is not null) await tx.CommitAsync(cancellationToken);
                SonKayit = Satir(not);
                completed = true;
                return ProgramTakipNotuSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    if (ownedTx is null) await tx.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    database.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Takip notu ekleme işlemi tamamlanamadı.");
            return ProgramTakipNotuSonucu.Basarisiz;
        }
    }

    public async Task<ProgramTakipNotuSonucu> DuzenleAsync(Guid actorId, ProgramTakipNotuDuzenleModeli model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || model is null || model.NotId == Guid.Empty || model.ProgramId == Guid.Empty ||
            string.IsNullOrWhiteSpace(model.Metin) || model.Metin.Length > 2000 || model.Surum is null || model.Surum.Length != 8 ||
            !Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true))
            return ProgramTakipNotuSonucu.Gecersiz;

        try
        {
            // Dış işlem kullanılırken aynı sürüm kontrolü korunur.
            if (database.Database.CurrentTransaction is { } callerTransaction &&
                callerTransaction.GetDbTransaction().IsolationLevel is not
                    (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
                return ProgramTakipNotuSonucu.Basarisiz;

            await using var ownedTx = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
            var tx = ownedTx ?? database.Database.CurrentTransaction!;
            var savepoint = Guid.NewGuid().ToString("N");
            if (ownedTx is null) await tx.CreateSavepointAsync(savepoint, cancellationToken);
            var completed = false;
            try
            {
                if (!await YetkiliMiAsync(actorId, cancellationToken)) return ProgramTakipNotuSonucu.IzinYok;

                var programGecerli = await database.OgrenciProgramlari.AsNoTracking().AnyAsync(p =>
                    p.Id == model.ProgramId && p.HedefBolum == "Bilgisayar Mühendisliği" &&
                    (p.Tur == OgrenciProgramTuru.Cap || p.Tur == OgrenciProgramTuru.YanDal), cancellationToken);
                if (!programGecerli) return ProgramTakipNotuSonucu.Bulunamadi;

                var not = await database.Set<ProgramTakipNotu>()
                    .FirstOrDefaultAsync(n => n.Id == model.NotId && n.ProgramId == model.ProgramId, cancellationToken);
                if (not is null || not.SilindiMi) return ProgramTakipNotuSonucu.Bulunamadi;

                await database.Entry(not).ReloadAsync(cancellationToken);
                if (database.Entry(not).State == EntityState.Detached || not.SilindiMi)
                    return ProgramTakipNotuSonucu.Bulunamadi;
                if (!not.RowVersion.SequenceEqual(model.Surum))
                    return ProgramTakipNotuSonucu.Cakisma;
                if (not.Metin == model.Metin)
                {
                    if (ownedTx is not null) await tx.CommitAsync(cancellationToken);
                    SonKayit = Satir(not);
                    completed = true;
                    return ProgramTakipNotuSonucu.Basarili;
                }

                var entry = database.Entry(not);
                entry.Property(n => n.RowVersion).OriginalValue = model.Surum;

                var oncekiMetin = not.Metin;
                var now = DateTimeOffset.UtcNow;
                not.Metin = model.Metin;
                not.DuzeltenKullaniciId = actorId;
                not.DuzenlemeAni = now;

                database.DegisiklikGecmisi.Add(new DegisiklikGecmisi
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(ProgramTakipNotu), VarlikAnahtari = not.Id.ToString(),
                    Islem = "ProgramTakipNotuDuzenle", YapanKullaniciId = actorId, IslemAni = now,
                    OncekiDeger = JsonSerializer.Serialize(new { not.ProgramId, Metin = oncekiMetin }),
                    YeniDeger = JsonSerializer.Serialize(new { not.ProgramId, not.Metin, not.DuzenlemeAni, not.DuzeltenKullaniciId, Gerekce = model.Gerekce.Trim() })
                });

                await database.SaveChangesAsync(cancellationToken);
                if (ownedTx is not null) await tx.CommitAsync(cancellationToken);
                SonKayit = Satir(not);
                completed = true;
                return ProgramTakipNotuSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    if (ownedTx is null) await tx.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    database.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            return ProgramTakipNotuSonucu.Cakisma;
        }
        catch (Exception)
        {
            logger.LogWarning("Takip notu düzenleme işlemi tamamlanamadı.");
            return ProgramTakipNotuSonucu.Basarisiz;
        }
    }

    public async Task<ProgramTakipNotuSonucu> SilAsync(Guid actorId, ProgramTakipNotuSilModeli model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || model is null || model.NotId == Guid.Empty || model.ProgramId == Guid.Empty ||
            model.Surum is null || model.Surum.Length != 8 ||
            !Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true))
            return ProgramTakipNotuSonucu.Gecersiz;
        try
        {
            if (database.Database.CurrentTransaction is { } caller &&
                caller.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
                return ProgramTakipNotuSonucu.Basarisiz;
            await using var ownedTx = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
            var tx = ownedTx ?? database.Database.CurrentTransaction!;
            var savepoint = Guid.NewGuid().ToString("N");
            if (ownedTx is null) await tx.CreateSavepointAsync(savepoint, cancellationToken);
            var completed = false;
            try
            {
                if (!await YetkiliMiAsync(actorId, cancellationToken)) return ProgramTakipNotuSonucu.IzinYok;
                if (!await database.OgrenciProgramlari.AsNoTracking().AnyAsync(p => p.Id == model.ProgramId &&
                    p.HedefBolum == "Bilgisayar Mühendisliği" &&
                    (p.Tur == OgrenciProgramTuru.Cap || p.Tur == OgrenciProgramTuru.YanDal), cancellationToken))
                    return ProgramTakipNotuSonucu.Bulunamadi;
                var not = await database.ProgramTakipNotlari.SingleOrDefaultAsync(n => n.Id == model.NotId &&
                    n.ProgramId == model.ProgramId, cancellationToken);
                if (not is null) return ProgramTakipNotuSonucu.Bulunamadi;
                await database.Entry(not).ReloadAsync(cancellationToken);
                if (database.Entry(not).State == EntityState.Detached || not.SilindiMi)
                    return ProgramTakipNotuSonucu.Bulunamadi;
                if (!not.RowVersion.SequenceEqual(model.Surum)) return ProgramTakipNotuSonucu.Cakisma;
                database.Entry(not).Property(n => n.RowVersion).OriginalValue = model.Surum;
                var now = DateTimeOffset.UtcNow;
                not.SilindiMi = true;
                not.SilenKullaniciId = actorId;
                not.SilmeAni = now;
                database.DegisiklikGecmisi.Add(new DegisiklikGecmisi
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(ProgramTakipNotu), VarlikAnahtari = not.Id.ToString(),
                    Islem = "ProgramTakipNotuIptal", YapanKullaniciId = actorId, IslemAni = now,
                    OncekiDeger = JsonSerializer.Serialize(new { not.ProgramId, not.Metin, not.OlusturmaAni }),
                    YeniDeger = JsonSerializer.Serialize(new { not.SilindiMi, not.SilmeAni, not.SilenKullaniciId, Gerekce = model.Gerekce.Trim() })
                });
                await database.SaveChangesAsync(cancellationToken);
                if (ownedTx is not null) await tx.CommitAsync(cancellationToken);
                SonKayit = null;
                completed = true;
                return ProgramTakipNotuSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    if (ownedTx is null) await tx.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    database.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return ProgramTakipNotuSonucu.Cakisma; }
        catch (Exception)
        {
            logger.LogWarning("Takip notu iptali tamamlanamadı.");
            return ProgramTakipNotuSonucu.Basarisiz;
        }
    }

    private async Task<bool> YetkiliMiAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return false;
        await database.Entry(actor).ReloadAsync(cancellationToken);
        var permission = await access.OkuAsync(actor);
        return permission.Izinli && !permission.ParolaDegisikligiGerekli && permission.Roller.Contains(UygulamaRolleri.Koordinator);
    }
}
