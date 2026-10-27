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

namespace CapYandal.Web.Services.Belgeler;

public enum BelgeIncelemeSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }

// Belge kararı verilirken kullanıcı, sürüm ve gerekçe yeniden doğrulanır.
public sealed class BelgeIncelemeServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<BelgeIncelemeServisi> logger)
{
    public Task<BelgeIncelemeSonucu> IsteAsync(Guid actorId, EkBelgeIsteModeli model, CancellationToken ct = default) =>
        IslemAsync(actorId, model, async () =>
        {
            var context = await BaglamiKilitleAsync(model.ProgramId, model.DonemId, ct);
            if (!context) return BelgeIncelemeSonucu.Bulunamadi;
            if (await db.EkBelgeIstekleri.AnyAsync(x => x.Id == model.IstekId, ct)) return BelgeIncelemeSonucu.Cakisma;
            var request = new EkBelgeIstegi { Id = model.IstekId, ProgramId = model.ProgramId, DonemId = model.DonemId,
                BelgeTuru = model.Tur!.Value, IstekMetni = string.IsNullOrWhiteSpace(model.Metin) ? BelgeArsivKurali.TurAdi(model.Tur.Value) + " isteniyor." : model.Metin.Trim(), IsteyenKullaniciId = actorId, IstekAni = clock.GetUtcNow() };
            db.EkBelgeIstekleri.Add(request);
            Audit(actorId, "EkBelgeIstegi", request.Id, "BelgeIste", null, IstekDegeri(request));
            return BelgeIncelemeSonucu.Basarili;
        }, ct);

    public Task<BelgeIncelemeSonucu> KararVerAsync(Guid actorId, BelgeKararModeli model, CancellationToken ct = default) =>
        IslemAsync(actorId, model, async () =>
        {
            // Bağlam keşfi kısa okuma kilidi kullanır; karar sırasında belge ve sürüm tekrar kilitlenir.
            var candidate = await db.Belgeler.FromSqlInterpolated($"SELECT * FROM [Belgeler] WITH (READCOMMITTED) WHERE [Id]={model.BelgeId}").AsNoTracking().SingleOrDefaultAsync(x => x.Id == model.BelgeId &&
                x.ProgramId == model.ProgramId && x.Kapsam == BelgeKapsami.Kisisel, ct);
            if (candidate is null) return BelgeIncelemeSonucu.Bulunamadi;
            var periodId = candidate.ArsivDonemId ?? (candidate.EvrakGonderimiId is { } submissionId
                ? await db.EvrakGonderimleri.Where(x => x.Id == submissionId).Select(x => (Guid?)x.DonemId).SingleOrDefaultAsync(ct)
                : await db.DersSecimleri.Where(x => x.Id == candidate.DersSecimiId).Select(x => (Guid?)x.DonemId).SingleOrDefaultAsync(ct));
            if (periodId is null || !await BaglamiKilitleAsync(model.ProgramId, periodId.Value, ct)) return BelgeIncelemeSonucu.Bulunamadi;
            EkBelgeIstegi? request = null;
            if (candidate.EkBelgeIstegiId is { } requestId)
            {
                request = await db.EkBelgeIstekleri.FromSqlInterpolated($"SELECT * FROM [EkBelgeIstekleri] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={requestId}").SingleOrDefaultAsync(ct);
                if (request is null || request.ProgramId != model.ProgramId || request.DonemId != periodId) return BelgeIncelemeSonucu.IzinYok;
                await db.Entry(request).ReloadAsync(ct);
            }
            KisiselBelgeSerisi? lockedSeries = null;
            if (candidate.KisiselBelgeSerisiId is { } seriesId)
            {
                var series = await db.KisiselBelgeSerileri.FromSqlInterpolated($"SELECT * FROM [KisiselBelgeSerileri] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={seriesId}").SingleOrDefaultAsync(ct);
                if (series is null || series.ProgramId != model.ProgramId) return BelgeIncelemeSonucu.IzinYok;
                if (await db.Belgeler.AnyAsync(x => x.KisiselBelgeSerisiId == seriesId && x.KisiselSurumNo > candidate.KisiselSurumNo, ct)) return BelgeIncelemeSonucu.Cakisma;
                lockedSeries = series;
            }
            var document = await db.Belgeler.FromSqlInterpolated($"SELECT * FROM [Belgeler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.BelgeId}").SingleOrDefaultAsync(ct);
            if (document is null) return BelgeIncelemeSonucu.Bulunamadi;
            await db.Entry(document).ReloadAsync(ct);
            if (document.ProgramId != model.ProgramId || document.Kapsam != BelgeKapsami.Kisisel) return BelgeIncelemeSonucu.IzinYok;
            if (!document.RowVersion.SequenceEqual(model.Surum) || document.OnayDurumu == model.Karar) return BelgeIncelemeSonucu.Cakisma;
            if (document.IcerikDurumu != BelgeIcerikDurumu.Mevcut || document.Sha256 is null || document.DepolamaAnahtari is null) return BelgeIncelemeSonucu.Gecersiz;
            if (document.OnayDurumu is not { } current || !OnayGecisKurali.KararGecerli(
                (KayitOnayDurumu)(int)current, (KayitOnayDurumu)(int)model.Karar!.Value, model.Gerekce)) return BelgeIncelemeSonucu.Gecersiz;
            var before = BelgeDegeri(document);
            if (lockedSeries is not null && !lockedSeries.OgrenciKilidi && model.Karar is BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.IptalEdildi)
            {
                lockedSeries.OgrenciKilidi = true;
                Audit(actorId, "KisiselBelgeSerisi", lockedSeries.Id, "OgrenciYenilemeKilidi", "{\"OgrenciKilidi\":false}", "{\"OgrenciKilidi\":true}");
            }
            document.OnayDurumu = model.Karar;
            document.KararGerekcesi = string.IsNullOrWhiteSpace(model.Gerekce) ? null : model.Gerekce.Trim();
            document.KararVerenKullaniciId = actorId; document.KararAni = clock.GetUtcNow();
            Audit(actorId, "Belge", document.Id, "BelgeKarari", before, BelgeDegeri(document));
            if (request is not null)
            {
                var requestBefore = IstekDegeri(request);
                request.Durum = model.Karar switch { BelgeOnayDurumu.Onaylandi => EkBelgeIstegiDurumu.Onaylandi,
                    BelgeOnayDurumu.Reddedildi => EkBelgeIstegiDurumu.Reddedildi, _ => EkBelgeIstegiDurumu.IptalEdildi };
                request.KararGerekcesi = document.KararGerekcesi; request.KararVerenKullaniciId = actorId; request.KararAni = document.KararAni;
                Audit(actorId, "EkBelgeIstegi", request.Id, "BelgeIstegiKarari", requestBefore, IstekDegeri(request));
            }
            return BelgeIncelemeSonucu.Basarili;
        }, ct);

    private async Task<bool> BaglamiKilitleAsync(Guid programId, Guid periodId, CancellationToken ct)
    {
        if (!await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={periodId}").AsNoTracking().AnyAsync(ct)) return false;
        return await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={programId}")
            .AsNoTracking().AnyAsync(p => p.HedefBolum == "Bilgisayar Mühendisliği", ct);
    }

    private async Task<BelgeIncelemeSonucu> IslemAsync(Guid actorId, object model, Func<Task<BelgeIncelemeSonucu>> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || !Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true)) return BelgeIncelemeSonucu.Gecersiz;
        if (db.ChangeTracker.HasChanges()) return BelgeIncelemeSonucu.Basarisiz;
        try
        {
            if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return BelgeIncelemeSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return BelgeIncelemeSonucu.IzinYok;
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return BelgeIncelemeSonucu.IzinYok;
                var result = await work();
                if (result != BelgeIncelemeSonucu.Basarili) return result;
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true; return result;
            }
            finally
            {
                if (!completed) { if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateException) { return BelgeIncelemeSonucu.Cakisma; }
        catch (Exception error) { logger.LogWarning("Belge incelemesi tamamlanamadı: {HataTuru}.", error.GetType().Name); return BelgeIncelemeSonucu.Basarisiz; }
    }

    private void Audit(Guid actor, string type, Guid id, string operation, string? before, string after) => db.DegisiklikGecmisi.Add(new()
    { Id = Guid.NewGuid(), VarlikTuru = type, VarlikAnahtari = id.ToString(), Islem = operation, YapanKullaniciId = actor, IslemAni = clock.GetUtcNow(), OncekiDeger = before, YeniDeger = after });
    private static string BelgeDegeri(Belge b) => JsonSerializer.Serialize(new { b.Id, b.ProgramId, b.KisiselSurumNo, b.OnayDurumu, b.KararGerekcesi, b.KararVerenKullaniciId, b.KararAni });
    internal static string IstekDegeri(EkBelgeIstegi r) => JsonSerializer.Serialize(new { r.Id, r.ProgramId, r.DonemId, r.BelgeTuru, r.IstekMetni, r.Durum, r.KararGerekcesi, r.KararVerenKullaniciId, r.KararAni });
}
