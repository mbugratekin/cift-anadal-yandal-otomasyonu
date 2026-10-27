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

namespace CapYandal.Web.Services.DersKaydi;

// Geçmiş dersler aktarılırken mevcut liste korunur.
public sealed partial class GecmisDersAktarimServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, DersDenemeServisi attempts, TimeProvider clock, ILogger<GecmisDersAktarimServisi> logger)
{
    public async Task<GecmisAktarimSecenekleri?> SeceneklerAsync(Guid actorId, Guid programId, CancellationToken ct)
    {
        if (!await KoordinatorMuAsync(actorId, ct)) return null;
        var program = await db.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId && x.HedefBolum == "Bilgisayar Mühendisliği", ct);
        if (program?.MufredatSablonId is not { } templateId) return null;
        var template = await db.DersSablonlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == templateId && x.EtkinMi && x.Tur == program.Tur &&
            x.HedefBolum == program.HedefBolum && ((x.Tur == OgrenciProgramTuru.YanDal && x.KaynakAnaDal == null) ||
            (x.Tur == OgrenciProgramTuru.Cap && x.KaynakAnaDal == program.KaynakAnaDal)), ct);
        var current = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
        if (template is null || current is null) return null;
        var periods = await db.Donemler.AsNoTracking().Where(x => !db.DersSecimleri.Any(l => l.ProgramId == programId && l.DonemId == x.Id) &&
            (x.AkademikYilBaslangici < current.AkademikYilBaslangici ||
            (x.AkademikYilBaslangici == current.AkademikYilBaslangici && (int)x.Yariyil < (int)current.Yariyil)))
            .OrderByDescending(x => x.AkademikYilBaslangici).ThenByDescending(x => x.Yariyil).ToListAsync(ct);
        var courses = await db.SablonDersleri.AsNoTracking().Where(x => x.SablonId == templateId && x.EtkinMi).OrderBy(x => x.Kod).ToListAsync(ct);
        return new(Convert.ToBase64String(program.RowVersion), Convert.ToBase64String(template.RowVersion),
            periods.Where(x => KabuldenOnceDegil(x, program)).Select(x => new GecmisAktarimDonemi(x.Id, x.Ad, Convert.ToBase64String(x.RowVersion))).ToList(),
            courses.Where(x => DersSnapshotKurali.Olustur(x, templateId, out _)).Select(x => new GecmisAktarimDersi(x.DersId, x.Kod!, x.Ad!)).ToList());
    }

    public async Task<DersDenemeCevabi> AktarAsync(Guid actorId, GecmisDersAktarimModeli model, CancellationToken ct)
    {
        static DersDenemeCevabi Result(DersDenemeSonucu value) => new(value);
        if (actorId == Guid.Empty || !Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true)) return Result(DersDenemeSonucu.Gecersiz);
        // Toplu aktarım kendi veritabanı işlemi içinde çalışır.
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges()) return Result(DersDenemeSonucu.Basarisiz);
        var initialTracked = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        var completed = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!await KoordinatorMuAsync(actorId, ct)) return Result(DersDenemeSonucu.IzinYok);
                var period = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.DonemId}").AsNoTracking().SingleOrDefaultAsync(ct);
                var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.ProgramId}").AsNoTracking().SingleOrDefaultAsync(ct);
                if (period is null || program?.HedefBolum != "Bilgisayar Mühendisliği" || program.MufredatSablonId is not { } templateId) return Result(DersDenemeSonucu.Bulunamadi);
                if (!KayitSurumKurali.KomutGuncel(program.Id, model.ProgramId, program.RowVersion, model.ProgramSurum) ||
                    !KayitSurumKurali.KomutGuncel(period.Id, model.DonemId, period.RowVersion, model.DonemSurum)) return Result(DersDenemeSonucu.Cakisma);
                var current = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
                if (current is null || !OncekiDonem(period, current) || !KabuldenOnceDegil(period, program)) return Result(DersDenemeSonucu.Gecersiz);
                if (await db.DersSecimleri.AnyAsync(x => x.ProgramId == program.Id && x.DonemId == period.Id, ct)) return Result(DersDenemeSonucu.Cakisma);
                var template = await db.DersSablonlari.FromSqlInterpolated($"SELECT * FROM [DersSablonlari] WITH (XLOCK,HOLDLOCK) WHERE [Id]={templateId} AND [Tur]={program.Tur} AND [HedefBolum]={program.HedefBolum} AND (([Tur]=2 AND [KaynakAnaDal] IS NULL) OR ([Tur]=1 AND [KaynakAnaDal]={program.KaynakAnaDal}))").AsNoTracking().SingleOrDefaultAsync(ct);
                if (template is null || !template.EtkinMi) return Result(DersDenemeSonucu.Gecersiz);
                if (!KayitSurumKurali.KomutGuncel(template.Id, templateId, template.RowVersion, model.SablonSurum)) return Result(DersDenemeSonucu.Cakisma);
                var ids = model.Satirlar.Select(x => x.DersId).ToArray();
                var catalog = await db.SablonDersleri.AsNoTracking().Where(x => x.SablonId == templateId && ids.Contains(x.DersId)).ToListAsync(ct);
                if (catalog.Count != ids.Length) return Result(DersDenemeSonucu.Gecersiz);
                var copies = new Dictionary<Guid, DersSnapshot>();
                foreach (var course in catalog)
                {
                    if (!DersSnapshotKurali.Olustur(course, templateId, out var copy)) return Result(DersDenemeSonucu.Gecersiz);
                    copies.Add(course.DersId, copy!);
                }
                var passedIds = model.Satirlar.Where(x => x.GecilenDonemId.HasValue).Select(x => x.GecilenDonemId!.Value).Distinct().ToArray();
                var passedPeriods = await db.Donemler.AsNoTracking().Where(x => passedIds.Contains(x.Id)).ToListAsync(ct);
                if (passedPeriods.Count != passedIds.Length || passedPeriods.Any(x => OncekiDonem(x, period) || !OncekiDonem(x, current))) return Result(DersDenemeSonucu.Gecersiz);
                var now = clock.GetUtcNow(); var reason = model.Gerekce.Trim();
                var list = new DersSecimi { Id = Guid.NewGuid(), ProgramId = program.Id, DonemId = period.Id, SablonId = templateId,
                    GonderenKullaniciId = actorId, GonderimAni = now, Durum = DersSecimiDurumu.Onaylandi, OgrenciKilidi = true, GecmisAktarimiMi = true,
                    OnaylayanKullaniciId = actorId, OnayAni = now, KararGerekcesi = reason };
                db.DersSecimleri.Add(list);
                var items = copies.Values.Select(copy => new DersSecimKalemi { Id = Guid.NewGuid(), ListeId = list.Id,
                    ProgramId = program.Id, DonemId = period.Id, SablonId = templateId, DersId = copy.DersId,
                    DersKoduKopyasi = copy.Kod, DersAdiKopyasi = copy.Ad, KrediKopyasi = copy.Kredi, AktsKopyasi = copy.Akts,
                    TurKopyasi = copy.Tur, MufredatYariyiliKopyasi = copy.MufredatYariyili, EkleyenKullaniciId = actorId, EklenmeAni = now }).ToArray();
                db.DersSecimKalemleri.AddRange(items);
                await DersListeSurumKaydi.EkleAsync(db, list, items, actorId, now, reason, ct);
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(DersSecimi), VarlikAnahtari = list.Id.ToString(),
                    Islem = "GecmisDersIlkAktarim", YapanKullaniciId = actorId, IslemAni = now,
                    YeniDeger = JsonSerializer.Serialize(new { Liste = list, Kalemler = items, Gerekce = reason, KatalogKopyalamaAni = now }) };
                db.DegisiklikGecmisi.Add(audit);
                await db.SaveChangesAsync(ct);
                foreach (var row in model.Satirlar)
                {
                    var response = await attempts.KaydetAsync(actorId, new() { ProgramId = program.Id, DonemId = period.Id,
                        DersSecimKalemiId = items.Single(x => x.DersId == row.DersId).Id, Durum = row.Durum, Not = row.Not, SayisalNot = row.SayisalNot,
                        GecilenDonemId = row.GecilenDonemId, OnayDurumu = KayitOnayDurumu.Onaylandi, GecmisAktarim = true, Gerekce = reason }, ct);
                    if (response.Sonuc != DersDenemeSonucu.Basarili) return response;
                }
                await tx.CommitAsync(ct); completed = true; return Result(DersDenemeSonucu.Basarili);
            }
            finally { if (!completed) await tx.RollbackAsync(CancellationToken.None); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return Result(DersDenemeSonucu.Cakisma); }
        catch (Exception ex) { logger.LogWarning("Geçmiş aktarımı tamamlanamadı: {HataTuru}.", ex.GetType().Name); return Result(DersDenemeSonucu.Basarisiz); }
        finally
        {
            if (!completed)
            {
                foreach (var entry in db.ChangeTracker.Entries().Where(x => !initialTracked.Contains(x.Entity)).ToArray())
                    entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<bool> KoordinatorMuAsync(Guid actorId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var actor = await users.FindByIdAsync(actorId.ToString()); if (actor is null) return false;
        await db.Entry(actor).ReloadAsync(ct); var permission = await access.OkuAsync(actor);
        return permission.Izinli && !permission.ParolaDegisikligiGerekli && permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator]);
    }
    private static bool OncekiDonem(Donem a, Donem b) => a.AkademikYilBaslangici < b.AkademikYilBaslangici ||
        (a.AkademikYilBaslangici == b.AkademikYilBaslangici && (int)a.Yariyil < (int)b.Yariyil);

    internal static bool KabuldenOnceDegil(Donem donem, OgrenciProgrami program) =>
        program.KabulAkademikYilBaslangici is not { } yil || program.KabulYariyili is not { } yariyil ||
        donem.AkademikYilBaslangici > yil ||
        donem.AkademikYilBaslangici == yil && (int)donem.Yariyil >= (int)yariyil;
}
