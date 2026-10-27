using System.ComponentModel.DataAnnotations;
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

public enum DersDenemeSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, TranskriptEksik, Basarisiz }
public sealed record DersDenemeCevabi(DersDenemeSonucu Sonuc, Guid? DenemeId = null, int? SurumNo = null);

// Not, karar sürümü ve işlem geçmişi birlikte kaydedilir.
public sealed class DersDenemeServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<DersDenemeServisi> logger)
{
    public async Task<DersDenemeCevabi> KaydetAsync(Guid actorId, DersDenemeModeli model, CancellationToken ct = default)
    {
        static DersDenemeCevabi Result(DersDenemeSonucu status) => new(status);
        ct.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || !Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true)) return Result(DersDenemeSonucu.Gecersiz);
        if (db.ChangeTracker.HasChanges()) return Result(DersDenemeSonucu.Basarisiz);
        var written = new List<object>();
        try
        {
            if (db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return Result(DersDenemeSonucu.Basarisiz);
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N"); var completed = false;
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return Result(DersDenemeSonucu.IzinYok);
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                var coordinator = permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator]);
                var student = permission.Roller.SequenceEqual([UygulamaRolleri.Ogrenci]);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || (!coordinator && !student)) return Result(DersDenemeSonucu.IzinYok);
                var period = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.DonemId}").AsNoTracking().SingleOrDefaultAsync(ct);
                var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.ProgramId}").AsNoTracking().SingleOrDefaultAsync(ct);
                if (period is null || program?.HedefBolum != "Bilgisayar Mühendisliği") return Result(DersDenemeSonucu.Bulunamadi);
                if (student && (program.OgrenciId != actorId || !program.AktifMi || model.GecmisAktarim ||
                    model.OnayDurumu != KayitOnayDurumu.OnayBekliyor || model.Durum != DersDenemeDurumu.Alindi || string.IsNullOrWhiteSpace(model.Not)))
                    return Result(DersDenemeSonucu.IzinYok);
                var snapshot = await db.DersSecimKalemleri.AsNoTracking().SingleOrDefaultAsync(x => x.Id == model.DersSecimKalemiId && x.ProgramId == model.ProgramId && x.DonemId == model.DonemId, ct);
                if (snapshot is null) return Result(DersDenemeSonucu.Bulunamadi);
                if (snapshot.IptalAni is not null && (student || model.DenemeId is null)) return Result(DersDenemeSonucu.IzinYok);
                var parentList = await db.DersSecimleri.AsNoTracking().SingleAsync(x => x.Id == snapshot.ListeId, ct);
                var historyList = parentList.GecmisAktarimiMi;
                if (model.OnayDurumu == KayitOnayDurumu.Onaylandi && parentList.Durum != DersSecimiDurumu.Onaylandi)
                    return Result(DersDenemeSonucu.Gecersiz);
                if (model.GecmisAktarim && !historyList) return Result(DersDenemeSonucu.Gecersiz);
                if (model.DenemeId is null && historyList && (model.OnayDurumu != KayitOnayDurumu.Onaylandi ||
                    string.IsNullOrWhiteSpace(model.Gerekce) || parentList.Durum != DersSecimiDurumu.Onaylandi || !parentList.OgrenciKilidi))
                    return Result(DersDenemeSonucu.Gecersiz);
                if (student)
                {
                    if (!await db.DersSecimleri.AnyAsync(x => x.Id == snapshot.ListeId && !x.GecmisAktarimiMi && x.Durum == DersSecimiDurumu.Onaylandi, ct)) return Result(DersDenemeSonucu.IzinYok);
                    var currentPeriod = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
                    if (currentPeriod is null || period.AkademikYilBaslangici > currentPeriod.AkademikYilBaslangici ||
                        (period.AkademikYilBaslangici == currentPeriod.AkademikYilBaslangici && (int)period.Yariyil >= (int)currentPeriod.Yariyil))
                        return Result(DersDenemeSonucu.Gecersiz);
                    var hasTranscript = await db.Belgeler.AnyAsync(b => b.ProgramId == model.ProgramId && b.ArsivDonemId == model.DonemId &&
                        b.Kapsam == BelgeKapsami.Kisisel && b.KisiselTur == KisiselBelgeTuru.CapYanDalTranskripti && b.KisiselBelgeSerisiId != null &&
                        b.IcerikDurumu == BelgeIcerikDurumu.Mevcut && b.DepolamaAnahtari != null && b.Sha256 != null &&
                        (b.OnayDurumu == BelgeOnayDurumu.OnayBekliyor || b.OnayDurumu == BelgeOnayDurumu.Onaylandi) &&
                        !db.Belgeler.Any(newer => newer.KisiselBelgeSerisiId == b.KisiselBelgeSerisiId && newer.KisiselSurumNo > b.KisiselSurumNo), ct);
                    if (!hasTranscript) return Result(DersDenemeSonucu.TranskriptEksik);
                }
                if (model.GecilenDonemId is { } passed)
                {
                    var passedPeriod = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.Id == passed, ct);
                    var current = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
                    if (!DersDonemKurali.GecilmeGecerli(period, passedPeriod, current)) return Result(DersDenemeSonucu.Gecersiz);
                }
                DersDenemesi? attempt = null; DersDenemeSurumu? previous = null;
                if (model.DenemeId is { } id)
                {
                    attempt = await db.DersDenemeleri.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
                    if (attempt is null || attempt.ProgramId != model.ProgramId || attempt.DonemId != model.DonemId || attempt.DersSecimKalemiId != snapshot.Id)
                        return Result(DersDenemeSonucu.IzinYok);
                    previous = await db.DersDenemeSurumleri.AsNoTracking().Where(x => x.DenemeId == id).OrderByDescending(x => x.SurumNo).FirstOrDefaultAsync(ct);
                    if (previous is null || previous.SurumNo != model.BeklenenSurum || previous.SurumNo == int.MaxValue) return Result(DersDenemeSonucu.Cakisma);
                    if (coordinator && (previous.Not != (string.IsNullOrWhiteSpace(model.Not) ? null : model.Not.Trim()) || previous.SayisalNot != model.SayisalNot) && string.IsNullOrWhiteSpace(model.Gerekce))
                        return Result(DersDenemeSonucu.Gecersiz);
                    if (student)
                    {
                        var locked = await db.DersDenemeSurumleri.AnyAsync(x => x.DenemeId == id && (x.OnayDurumu == KayitOnayDurumu.Onaylandi || x.OnayDurumu == KayitOnayDurumu.IptalEdildi), ct);
                        if (!OnayGecisKurali.OgrenciGonderebilir(previous.OnayDurumu, locked)) return Result(DersDenemeSonucu.IzinYok);
                    }
                    else if (((previous.OnayDurumu == model.OnayDurumu || model.OnayDurumu == KayitOnayDurumu.OnayBekliyor) && string.IsNullOrWhiteSpace(model.Gerekce)) || (model.OnayDurumu != KayitOnayDurumu.OnayBekliyor &&
                        previous.OnayDurumu != model.OnayDurumu && !OnayGecisKurali.KararGecerli(previous.OnayDurumu, model.OnayDurumu, model.Gerekce)))
                        return Result(DersDenemeSonucu.Gecersiz);
                }
                else
                {
                    if (await db.DersDenemeleri.AnyAsync(x => x.DersSecimKalemiId == snapshot.Id, ct)) return Result(DersDenemeSonucu.Cakisma);
                    if (coordinator && string.IsNullOrWhiteSpace(model.Gerekce)) return Result(DersDenemeSonucu.Gecersiz);
                    attempt = new() { Id = Guid.NewGuid(), ProgramId = model.ProgramId, DonemId = model.DonemId, DersSecimKalemiId = snapshot.Id,
                        Kaynak = student ? DersKayitKaynagi.Ogrenci : historyList ? DersKayitKaynagi.GecmisAktarim : DersKayitKaynagi.Koordinator,
                        OlusturanKullaniciId = actorId, OlusturmaAni = clock.GetUtcNow() };
                    db.DersDenemeleri.Add(attempt);
                    written.Add(attempt);
                }
                var revision = new DersDenemeSurumu { Id = Guid.NewGuid(), DenemeId = attempt.Id, SurumNo = (previous?.SurumNo ?? 0) + 1,
                    Durum = model.Durum, Not = string.IsNullOrWhiteSpace(model.Not) ? null : model.Not.Trim(), SayisalNot = model.SayisalNot, GecilenDonemId = model.GecilenDonemId,
                    OnayDurumu = model.OnayDurumu, Gerekce = string.IsNullOrWhiteSpace(model.Gerekce) ? null : model.Gerekce.Trim(),
                    YapanKullaniciId = actorId, IslemAni = clock.GetUtcNow() };
                db.DersDenemeSurumleri.Add(revision);
                written.Add(revision);
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(DersDenemesi), VarlikAnahtari = attempt.Id.ToString(),
                    Islem = previous is null ? "DersDenemesiKaydet" : "DersDenemesiSurumle", YapanKullaniciId = actorId, IslemAni = revision.IslemAni,
                    OncekiDeger = previous is null ? null : JsonSerializer.Serialize(previous), YeniDeger = JsonSerializer.Serialize(new { Deneme = attempt, Surum = revision }) };
                db.DegisiklikGecmisi.Add(audit); written.Add(audit);
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true; return new(DersDenemeSonucu.Basarili, attempt.Id, revision.SurumNo);
            }
            finally
            {
                if (!completed) { if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None);
                    foreach (var row in written) db.Entry(row).State = EntityState.Detached; }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return Result(DersDenemeSonucu.Cakisma); }
        catch (Exception error) { logger.LogWarning("Ders denemesi kaydedilemedi: {HataTuru}.", error.GetType().Name); return Result(DersDenemeSonucu.Basarisiz); }
    }
}
