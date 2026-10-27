using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Gano;

public sealed record SayisalKontrolKopyasi(Guid ProgramId, Guid DonemId, Guid SurumId, int SurumNo,
    DateTimeOffset KontrolAni, IReadOnlyList<SayisalKuralSonucu> Sonuclar);
public sealed record SayisalKontrolCevabi(int Durum, SayisalKontrolKopyasi? Kontrol = null, bool YeniKuralVar = false);

// Sayısal koşulun kullanılan kuralını ve sonucunu geçmişte saklar.
public sealed class SayisalKosulServisi(AppDbContext db, HesapErisimi erisim, TimeProvider saat, ILogger<SayisalKosulServisi> logger)
{
    private async Task<OgrenciProgrami?> YetkiAsync(Guid actor, Guid programId, bool yaz, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, ct);
        if (user is null) return null;
        var p = await erisim.OkuAsync(user);
        if (!p.Izinli || p.ParolaDegisikligiGerekli || p.Roller.Length != 1) return null;
        var program = await db.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId && x.HedefBolum == "Bilgisayar Mühendisliği", ct);
        if (program is null) return null;
        return p.Roller[0] == UygulamaRolleri.Koordinator || !yaz && (p.Roller[0] == UygulamaRolleri.BolumBaskani ||
            p.Roller[0] == UygulamaRolleri.Ogrenci && program.OgrenciId == actor && program.AktifMi) ? program : null;
    }
    public async Task<SayisalKontrolCevabi> OkuAsync(Guid actor, Guid programId, Guid donemId, CancellationToken ct)
    {
        var program = await YetkiAsync(actor, programId, false, ct);
        if (program is null) return new(403);
        // Güncel dönem ekranda canlı değerlendirilir; GET denetim geçmişine yazmaz.
        if (await db.Donemler.AsNoTracking().AnyAsync(x => x.Id == donemId && x.IslemDonemiMi, ct))
            return await DegerlendirAsync(actor, programId, donemId, false, ct);
        var son = await SonAsync(programId, donemId, ct);
        var version = await db.AkademikKosulSurumleri.AsNoTracking().Where(x => x.ProgramTuru == program.Tur && x.TamamlandiMi)
            .OrderByDescending(x => x.SurumNo).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return new(200, son, son is not null && version != son.SurumId);
    }
    public Task<SayisalKontrolCevabi> KontrolAsync(Guid actor, Guid programId, Guid donemId, CancellationToken ct) =>
        DegerlendirAsync(actor, programId, donemId, true, ct);

    private async Task<SayisalKontrolCevabi> DegerlendirAsync(Guid actor, Guid programId, Guid donemId, bool kaydet, CancellationToken ct)
    {
        if (actor == Guid.Empty || programId == Guid.Empty || donemId == Guid.Empty) return new(400);
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return new(503);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var program = await YetkiAsync(actor, programId, kaydet, ct);
            if (program is null) return new(403);
            var period = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.Id == donemId, ct);
            if (period is null) return new(400);
            var current = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
            if (current is null || period.AkademikYilBaslangici > current.AkademikYilBaslangici || period.AkademikYilBaslangici == current.AkademikYilBaslangici && period.Yariyil > current.Yariyil) return new(409);
            var old = await SonAsync(programId, donemId, ct);
            if (old is null && period.Id != current.Id) return new(409);
            var version = period.Id == current.Id
                ? await db.AkademikKosulSurumleri.AsNoTracking().Where(x => x.ProgramTuru == program.Tur).OrderByDescending(x => x.SurumNo).FirstOrDefaultAsync(ct)
                : await db.AkademikKosulSurumleri.AsNoTracking().SingleOrDefaultAsync(x => x.Id == old!.SurumId && x.ProgramTuru == program.Tur, ct);
            if (version is null || !version.TamamlandiMi) return new(409);
            var rules = SayisalKuralKurali.Oku(version.SayisalKurallarJson);
            if (rules is null) return new(409);
            var periods = await db.Donemler.AsNoTracking().Where(x => x.AkademikYilBaslangici < period.AkademikYilBaslangici ||
                x.AkademikYilBaslangici == period.AkademikYilBaslangici && x.Yariyil <= period.Yariyil).Select(x => x.Id).ToArrayAsync(ct);
            var lists = await db.DersSecimleri.AsNoTracking().Where(x => x.ProgramId == programId && periods.Contains(x.DonemId) && x.Durum == DersSecimiDurumu.Onaylandi).ToListAsync(ct);
            var listIds = lists.Select(x => x.Id).ToArray();
            var items = await db.DersSecimKalemleri.AsNoTracking().Where(x => listIds.Contains(x.ListeId)).ToListAsync(ct);
            var attempts = await db.DersDenemeleri.AsNoTracking().Where(x => x.ProgramId == programId && periods.Contains(x.DonemId)).ToDictionaryAsync(x => x.DersSecimKalemiId, ct);
            var attemptIds = attempts.Values.Select(x => x.Id).ToArray();
            var grades = await db.DersDenemeSurumleri.AsNoTracking().Where(x => attemptIds.Contains(x.DenemeId) &&
                !db.DersDenemeSurumleri.Any(y => y.DenemeId == x.DenemeId && y.SurumNo > x.SurumNo)).ToDictionaryAsync(x => x.DenemeId, ct);
            var progress = DersIlerlemeKurali.Hesapla(items.Select(x =>
            {
                var attempt = attempts.GetValueOrDefault(x.Id); var grade = attempt is null ? null : grades.GetValueOrDefault(attempt.Id);
                return new DersIlerlemeGirdisi(x.Id, x.DersId, x.DersKoduKopyasi, x.DersAdiKopyasi, x.KrediKopyasi, x.AktsKopyasi,
                    true, x.IptalAni.HasValue, attempt?.Id, grade?.OnayDurumu, grade?.Durum);
            }), 0);
            int? elapsed = program.KabulAkademikYilBaslangici is int yil && program.KabulYariyili is { } yariyil
                ? (period.AkademikYilBaslangici - yil) * 2 + (int)period.Yariyil - (int)yariyil : null;
            if (elapsed < 0) return new(400);
            SayisalEkVeri? extra = null;
            if (rules.Any(x => x.Olcut is SayisalOlcut.HavuzAkts or SayisalOlcut.SecilenDers or SayisalOlcut.SecilenAkts))
            {
                var selected = DersIlerlemeKurali.Hesapla(items.Select(x => new DersIlerlemeGirdisi(x.Id, x.DersId,
                    x.DersKoduKopyasi, x.DersAdiKopyasi, x.KrediKopyasi, x.AktsKopyasi, true, x.IptalAni.HasValue, null, null, null)), 0);
                decimal? pool = null;
                if (rules.Any(x => x.Olcut == SayisalOlcut.HavuzAkts))
                {
                    if (period.Id != current.Id) pool = old?.Sonuclar.FirstOrDefault(x => x.Olcut == SayisalOlcut.HavuzAkts)?.Deger;
                    else if (program.MufredatSablonId is Guid template)
                    {
                        var values = await (from c in db.SablonDersleri.AsNoTracking() join d in db.Dersler.AsNoTracking() on c.DersId equals d.Id
                            where c.SablonId == template && c.EtkinMi select c.Akts ?? (decimal?)d.Akts).ToArrayAsync(ct);
                        if (values.Length > 0 && values.All(x => x.HasValue)) pool = values.Sum(x => x!.Value);
                    }
                }
                var coherent = lists.Count > 0 && !selected.Dersler.Any(x => x.KopyaCeliskisi);
                extra = new(pool, coherent ? selected.Dersler.Count : null,
                    coherent && selected.Dersler.All(x => x.Akts.HasValue) ? selected.Dersler.Sum(x => x.Akts!.Value) : null);
            }
            int? kayitsiz = null;
            if (rules.Any(x => x.Olcut == SayisalOlcut.ArdisikKayitsizDonem))
            {
                var periodCodes = await db.Donemler.AsNoTracking().Where(x => periods.Contains(x.Id)).ToDictionaryAsync(x => x.Id,
                    x => x.AkademikYilBaslangici * 2 + (int)x.Yariyil - 1, ct);
                var approvedListIds = items.Where(x => x.IptalAni == null).Select(x => x.ListeId).ToHashSet();
                var approvedTerms = lists.Where(x => approvedListIds.Contains(x.Id)).Select(x => periodCodes[x.DonemId]).ToHashSet();
                var pendingIds = await db.DersSecimleri.AsNoTracking().Where(x => x.ProgramId == programId && periods.Contains(x.DonemId) &&
                    x.Durum == DersSecimiDurumu.OnayBekliyor).Select(x => x.DonemId).ToArrayAsync(ct);
                var leave = await db.ProgramDonemIzinleri.AsNoTracking().Where(x => x.ProgramId == programId && x.IptalAni == null)
                    .Select(x => new { x.Baslangic, x.Bitis }).ToListAsync(ct);
                kayitsiz = DersKayitBoslukKurali.Say(program.KabulAkademikYilBaslangici is int ky && program.KabulYariyili is { } ks ? ky * 2 + (int)ks - 1 : null,
                    period.AkademikYilBaslangici * 2 + (int)period.Yariyil - 1, approvedTerms,
                    pendingIds.Select(x => periodCodes[x]).ToHashSet(), leave.Select(x => (x.Baslangic, x.Bitis)).ToArray());
            }
            var result = new SayisalKontrolKopyasi(program.Id, period.Id, version.Id, version.SurumNo, saat.GetUtcNow(),
                rules.Where(x => x.MufredatId is null || program.MufredatSablonId is null || x.MufredatId == program.MufredatSablonId)
                    .Select(x => SayisalKuralKurali.Degerlendir(x, progress, lists.Count > 0, elapsed,
                        x.MufredatId is null || x.MufredatId == program.MufredatSablonId && lists.All(l => l.SablonId == x.MufredatId), kayitsiz, extra)).ToArray());
            if (!kaydet) return new(200, result);
            var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(OgrenciProgrami), VarlikAnahtari = $"{programId}/{donemId}",
                Islem = "SayisalKosulKontrolu", YapanKullaniciId = actor, IslemAni = result.KontrolAni,
                OncekiDeger = old is null ? null : JsonSerializer.Serialize(old), YeniDeger = JsonSerializer.Serialize(result) };
            db.DegisiklikGecmisi.Add(audit);
            try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
            finally { db.Entry(audit).State = EntityState.Detached; }
            return new(200, result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) { logger.LogWarning("Sayısal koşul kontrolü tamamlanamadı: {Tur}", error.GetType().Name); return new(503); }
    }
    private async Task<SayisalKontrolKopyasi?> SonAsync(Guid id, Guid period, CancellationToken ct)
    {
        var key = $"{id}/{period}";
        var json = await db.DegisiklikGecmisi.AsNoTracking().Where(x => x.VarlikTuru == nameof(OgrenciProgrami) && x.Islem == "SayisalKosulKontrolu" && x.VarlikAnahtari == key)
            .OrderByDescending(x => x.IslemAni).ThenByDescending(x => x.Id).Select(x => x.YeniDeger).FirstOrDefaultAsync(ct);
        if (json is null) return null;
        return JsonSerializer.Deserialize<SayisalKontrolKopyasi>(json);
    }
}
