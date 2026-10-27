using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.DersKaydi;
public enum KayitKapiSonucu { Basarili, Gecersiz, IzinYok, Cakisma, Basarisiz }

public sealed class KayitKapisiServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<KayitKapisiServisi> logger)
{
    public Task<KayitKapiSonucu> KaydetAsync(Guid actorId, KayitKapisiModel model, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || model.Yil is < 2 or > 9998 || model.Yariyil is < 1 or > 2 || model.Niyet is < 1 or > 2 ||
            model.Surum?.Length > 16) return Task.FromResult(KayitKapiSonucu.Gecersiz);
        return IslemAsync(async written =>
        {
            var actor = await users.FindByIdAsync(actorId.ToString());
            if (actor is null) return KayitKapiSonucu.IzinYok;
            await db.Entry(actor).ReloadAsync(ct);
            var permission = await access.OkuAsync(actor);
            if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator)) return KayitKapiSonucu.IzinYok;
            var year = model.Yariyil == (int)AkademikYariyil.Bahar ? model.Yil - 1 : model.Yil;
            var semester = (AkademikYariyil)model.Yariyil;
            var period = await db.Donemler.FromSqlInterpolated(
                $"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [AkademikYilBaslangici] = {year} AND [Yariyil] = {semester}").AsNoTracking().SingleOrDefaultAsync(ct);
            if (period is null)
            {
                if (model.Niyet != 1 || !string.IsNullOrEmpty(model.Surum)) return KayitKapiSonucu.Gecersiz;
                // GANO dönem eşlemesiyle aynıdır. İşlem dönemi/öğrenci yarıyılı değiştirilmez.
                period = new() { Id = Guid.NewGuid(), AkademikYilBaslangici = year, Yariyil = semester,
                    Ad = $"{model.Yil} {(semester == AkademikYariyil.Guz ? "Güz" : "Bahar")}", Kod = $"{year}-{model.Yariyil}" };
                db.Donemler.Add(period); written.Add(period);
            }
            var gate = await KapiOkuAsync(period.Id, ct);
            var now = clock.GetUtcNow();
            var old = gate is null ? new KapiDurumu(period.Id, false) : Durum(gate);
            if (gate is null && (!string.IsNullOrEmpty(model.Surum) || model.Niyet != 1)) return KayitKapiSonucu.Cakisma;
            if (gate is not null && !KayitSurumKurali.KomutGuncel(gate.DonemId, period.Id, gate.RowVersion, model.Surum)) return KayitKapiSonucu.Cakisma;
            KapiDurumu next;
            var valid = model.Niyet == 1
                ? KayitZamaniKurali.Baslat(old, period.Id, now, model.Baslangic, model.Bitis, out next)
                : KayitZamaniKurali.Kapat(old, period.Id, now, model.Bitis, out next);
            if (!valid) return KayitKapiSonucu.Gecersiz;
            if (gate is null) { gate = new() { DonemId = period.Id }; db.DersKayitKapilari.Add(gate); written.Add(gate); }
            else { TakipEt(gate); written.Add(gate); }
            if (old == next && gate.RowVersion.Length == 8) return KayitKapiSonucu.Basarili;
            Ata(gate, next); gate.PlanlayanKullaniciId = actorId;
            Gecmis(gate, old, next, actorId, now, "KayitKapisiYonetildi", written);
            return KayitKapiSonucu.Basarili;
        }, ct);
    }

    public Task<KayitKapiSonucu> EsikleriUygulaAsync(Guid donemId, CancellationToken ct = default)
    {
        if (donemId == Guid.Empty) return Task.FromResult(KayitKapiSonucu.Gecersiz);
        return IslemAsync(async written =>
        {
            // Manuel komut/gönderimle aynı ebeveyn -> kapı kilit sırası.
            var parent = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {donemId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (parent is null) return KayitKapiSonucu.Gecersiz;
            var gate = await KapiOkuAsync(donemId, ct);
            if (gate is null) return KayitKapiSonucu.Basarili; // Kapı yoksa açık sayılmaz.
            var now = clock.GetUtcNow(); var old = Durum(gate); var next = KayitKapisiKurali.EsikleriUygula(old, now);
            if (old == next) return KayitKapiSonucu.Basarili;
            if (gate.PlanlayanKullaniciId is not { } actor) return KayitKapiSonucu.Basarisiz;
            TakipEt(gate); written.Add(gate); Ata(gate, next);
            Gecmis(gate, old, next, actor, now, "KayitKapisiPlaniUygulandi", written);
            return KayitKapiSonucu.Basarili;
        }, ct);
    }

    private Task<DersKayitKapisi?> KapiOkuAsync(Guid id, CancellationToken ct) => db.DersKayitKapilari
        .FromSqlInterpolated($"SELECT * FROM [DersKayitKapilari] WITH (UPDLOCK,HOLDLOCK) WHERE [DonemId] = {id}")
        .AsNoTracking().SingleOrDefaultAsync(ct);
    public static KapiDurumu Durum(DersKayitKapisi gate) => new(gate.DonemId, gate.AcikMi, gate.AcilisAniUtc, gate.KapanisAniUtc);
    private static void Ata(DersKayitKapisi gate, KapiDurumu next)
    { gate.AcikMi = next.AcikMi; gate.AcilisAniUtc = next.AcilisAniUtc; gate.KapanisAniUtc = next.KapanisAniUtc; }
    private void TakipEt(DersKayitKapisi gate)
    {
        var tracked = db.ChangeTracker.Entries<DersKayitKapisi>().SingleOrDefault(e => e.Entity.DonemId == gate.DonemId);
        if (tracked is not null) tracked.State = EntityState.Detached;
        db.Attach(gate);
    }
    private void Gecmis(DersKayitKapisi gate, KapiDurumu old, KapiDurumu next, Guid actor, DateTimeOffset now, string operation, List<object> written)
    {
        var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(DersKayitKapisi), VarlikAnahtari = gate.DonemId.ToString(),
            YapanKullaniciId = actor, IslemAni = now, Islem = operation, OncekiDeger = JsonSerializer.Serialize(old), YeniDeger = JsonSerializer.Serialize(next) };
        db.DegisiklikGecmisi.Add(audit); written.Add(audit);
    }
    private async Task<KayitKapiSonucu> IslemAsync(Func<List<object>, Task<KayitKapiSonucu>> operation, CancellationToken ct)
    {
        if (db.ChangeTracker.HasChanges()) return KayitKapiSonucu.Basarisiz;
        var written = new List<object>(); bool complete = false;
        try
        {
            if (db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return KayitKapiSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                var result = await operation(written);
                if (result != KayitKapiSonucu.Basarili) return result;
                await db.SaveChangesAsync(ct);
                if (own is not null) await own.CommitAsync(ct);
                complete = true; return result;
            }
            finally
            {
                if (!complete)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await own.RollbackAsync(CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return KayitKapiSonucu.Cakisma; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return KayitKapiSonucu.Cakisma; }
        catch (Exception ex)
        {
            logger.LogWarning("Kayıt kapısı işlemi tamamlanamadı. Tür: {HataTuru}, SQL: {Kod}", ex.GetType().Name, (ex.GetBaseException() as SqlException)?.Number);
            return KayitKapiSonucu.Basarisiz;
        }
        finally { if (!complete) foreach (var row in written) db.Entry(row).State = EntityState.Detached; }
    }
}
