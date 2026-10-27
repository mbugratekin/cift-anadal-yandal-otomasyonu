using System.Data;
using System.Text.Json;
using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.DersKaydi;

public sealed partial class DersOnayServisi
{
    public async Task<DersOnaySonucu> DuzeltmeIsteAsync(Guid actor, DersOnayGirdisi input, Guid[] dersler, CancellationToken ct)
    {
        if (dersler.Length is < 1 or > 1000 || dersler.Any(x => x == Guid.Empty) || dersler.Distinct().Count() != dersler.Length ||
            input.Karar != DersSecimiDurumu.Reddedildi) return DersOnaySonucu.Gecersiz;
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges()) return DersOnaySonucu.Basarisiz;
        var initial = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        bool complete = false;
        try
        {
            var user = await users.FindByIdAsync(actor.ToString());
            if (user is null) return DersOnaySonucu.IzinYok;
            await db.Entry(user).ReloadAsync(ct); var permission = await access.OkuAsync(user);
            if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(CapYandal.Web.Security.UygulamaRolleri.Koordinator)) return DersOnaySonucu.IzinYok;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var old = await db.DersSecimleri.FromSqlInterpolated($"SELECT * FROM [DersSecimleri] WITH (READUNCOMMITTED) WHERE [Id]={input.ListeId}").AsNoTracking().SingleOrDefaultAsync(ct);
                if (old is null) return DersOnaySonucu.Bulunamadi;
                if (old.OgrenciKilidi || old.Durum is DersSecimiDurumu.Onaylandi or DersSecimiDurumu.IptalEdildi) return DersOnaySonucu.Gecersiz;
                var decision = await OnaylaAsync(actor, input, ct); // Yetki, üst kayıt kilitleri ve sürüm bu servisçe doğrulanır.
                if (decision != DersOnaySonucu.Basarili) return decision;
                if (!await db.OgrenciProgramlari.AnyAsync(x => x.Id == old.ProgramId && x.AktifMi, ct)) return DersOnaySonucu.Gecersiz;
                if (await db.DersDuzeltmeTalepleri.AnyAsync(x => x.ListeId == old.Id && x.KapatilmaAni == null, ct)) return DersOnaySonucu.Cakisma;
                var items = await db.DersSecimKalemleri.AsNoTracking().Where(x => x.ListeId == old.Id && x.IptalAni == null && dersler.Contains(x.DersId)).ToListAsync(ct);
                if (items.Count != dersler.Length) return DersOnaySonucu.Gecersiz;
                var now = clock.GetUtcNow();
                db.DersDuzeltmeTalepleri.AddRange(items.Select(x => new DersDuzeltmeTalebi { Id = Guid.NewGuid(), ListeId = old.Id, KalemId = x.Id,
                    IsteyenKullaniciId = actor, IstekAni = now, Gerekce = input.Gerekce!.Trim() }));
                db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(DersDuzeltmeTalebi), VarlikAnahtari = old.Id.ToString(),
                    Islem = "DersBazindaDuzeltmeIste", YapanKullaniciId = actor, IslemAni = now,
                    YeniDeger = JsonSerializer.Serialize(new { Dersler = items.Select(x => new { x.Id, x.DersId, x.DersKoduKopyasi }), input.Gerekce }) });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); complete = true; return DersOnaySonucu.Basarili;
            }
            finally { if (!complete) await tx.RollbackAsync(CancellationToken.None); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return DersOnaySonucu.Cakisma; }
        catch (Exception ex) { logger.LogWarning("Ders düzeltme isteği kaydedilemedi: {HataTuru}.", ex.GetType().Name); return DersOnaySonucu.Basarisiz; }
        finally { if (!complete) foreach (var e in db.ChangeTracker.Entries().Where(x => !initial.Contains(x.Entity)).ToArray()) e.State = EntityState.Detached; }
    }
}
