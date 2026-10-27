using System.Data;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.DersKaydi;

// Onay başarısız olursa ders düzeltmesi de geri alınır.
public static class DersListeBirlesikKarar
{
    public static async Task<DersListeCevabi> KaydetVeOnaylaAsync(AppDbContext db, DersListeServisi lists,
        DersOnayServisi approvals, Guid actor, DersDuzeltGirdisi input, ILogger logger, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges()) return new(DersListeSonucu.Basarisiz);
        var initial = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        bool complete = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var corrected = await lists.DuzeltAsync(actor, input, ct);
                if (corrected.Sonuc != DersListeSonucu.Basarili) return corrected;
                var version = await db.DersSecimleri.AsNoTracking().Where(x => x.Id == input.ListeId).Select(x => x.RowVersion).SingleAsync(ct);
                var decision = await approvals.OnaylaAsync(actor, new(input.ListeId, Convert.ToBase64String(version)), ct);
                if (decision != DersOnaySonucu.Basarili) return new(decision switch {
                    DersOnaySonucu.Cakisma => DersListeSonucu.Cakisma, DersOnaySonucu.Gecersiz => DersListeSonucu.Gecersiz,
                    DersOnaySonucu.IzinYok => DersListeSonucu.IzinYok, DersOnaySonucu.Bulunamadi => DersListeSonucu.Bulunamadi,
                    _ => DersListeSonucu.Basarisiz });
                await tx.CommitAsync(ct); complete = true; return corrected;
            }
            finally { if (!complete) await tx.RollbackAsync(CancellationToken.None); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return new(DersListeSonucu.Cakisma); }
        catch (Exception ex)
        {
            logger.LogWarning("Birleşik ders düzeltme/onay tamamlanamadı: {HataTuru}.", ex.GetType().Name);
            return new(DersListeSonucu.Basarisiz);
        }
        finally { if (!complete) foreach (var entry in db.ChangeTracker.Entries().Where(x => !initial.Contains(x.Entity)).ToArray()) entry.State = EntityState.Detached; }
    }
}
