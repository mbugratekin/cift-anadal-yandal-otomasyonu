using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Gano;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Ogrenci;

public enum GanoKayitSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz, DegerlendirmeTutarsiz }

public sealed class GanoKayitServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, ILogger<GanoKayitServisi> logger, TimeProvider? clock = null)
{
    // Ekran takvim yilidir; eski Donem akademik yil baslangicini saklamaya devam eder.
    public static bool DonemiOku(string? metin, out int yil, out AkademikYariyil yariyil)
    {
        yil = 0; yariyil = default;
        var match = Regex.Match(metin ?? "", @"^([0-9]{4}) (Bahar|Güz)$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out yil) || yil is < 2 or > 9998) return false;
        yariyil = match.Groups[2].Value == "Bahar" ? AkademikYariyil.Bahar : AkademikYariyil.Guz;
        return true;
    }

    public static bool GanoyuOku(string? metin, out decimal? deger)
    {
        deger = null;
        if (string.IsNullOrWhiteSpace(metin)) return true;
        var text = metin.Trim();
        if (!Regex.IsMatch(text, @"^[0-4]([.,][0-9]{1,2})?$", RegexOptions.CultureInvariant) ||
            !decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value > 4) return false;
        deger = value;
        return true;
    }

    public static string? SonrakiDonem(int akademikYil, AkademikYariyil yariyil)
    {
        var yil = akademikYil + 1;
        return yil is < 2 or > 9998 ? null : $"{yil} {(yariyil == AkademikYariyil.Guz ? "Bahar" : "Güz")}";
    }

    public static bool IlkDegerlendirmeDonemiMi(int takvimYili, AkademikYariyil yariyil, DateOnly today, Donem? donem)
    {
        var academicYear = yariyil == AkademikYariyil.Bahar ? takvimYili - 1 : takvimYili;
        var currentDates = donem?.BaslangicTarihi is DateOnly start && donem.BitisTarihi is DateOnly end &&
            start <= today && today <= end;
        return academicYear >= today.Year || currentDates || (takvimYili == today.Year && donem?.IslemDonemiMi == true);
    }

    public async Task<GanoKayitSonucu> KaydetAsync(Guid actorId, GanoKaydetModeli model, CancellationToken ct = default)
    {
        var source = string.IsNullOrWhiteSpace(model.AnaDalKaynak) ? null : model.AnaDalKaynak.Trim();
        if (model.AnaDalKaynak?.Length > 500 || model.Gerekce?.Length > 2000) return GanoKayitSonucu.Gecersiz;
        var reason = string.IsNullOrWhiteSpace(model.Gerekce) ? null : model.Gerekce.Trim();
        if (db.ChangeTracker.HasChanges()) return GanoKayitSonucu.Basarisiz;
        if (actorId == Guid.Empty || model.ProgramId == Guid.Empty || !DonemiOku(model.Donem, out var yil, out var yariyil) ||
            !GanoyuOku(model.AnaDalGano, out var anaDal) || !GanoyuOku(model.ProgramGano, out var programGano) ||
            (anaDal is null && programGano is null)) return GanoKayitSonucu.Gecersiz;

        return await IslemAsync(async () =>
        {
            if (!await YetkiliMiAsync(actorId, ct)) return GanoKayitSonucu.IzinYok;
            var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.ProgramId}").AsNoTracking().SingleOrDefaultAsync(p =>
                p.Id == model.ProgramId && p.HedefBolum == "Bilgisayar Mühendisliği", ct);
            if (program is null || program.Tur is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal)) return GanoKayitSonucu.Bulunamadi;
            var student = await users.FindByIdAsync(program.OgrenciId.ToString());
            if (student is null || !await users.IsInRoleAsync(student, UygulamaRolleri.Ogrenci)) return GanoKayitSonucu.Bulunamadi;
            var akademikYil = yariyil == AkademikYariyil.Bahar ? yil - 1 : yil;
            var donem = await db.Donemler.SingleOrDefaultAsync(d => d.AkademikYilBaslangici == akademikYil && d.Yariyil == yariyil, ct);
            var existing = donem is null ? [] : await db.GanoKontrolKayitlari
                .Where(g => g.ProgramId == model.ProgramId && g.DonemId == donem.Id).ToListAsync(ct);
            foreach (var row in existing) await db.Entry(row).ReloadAsync(ct);
            var kapsam = program.Tur == OgrenciProgramTuru.Cap ? GanoKapsami.Cap : GanoKapsami.YanDal;
            var anaKayit = existing.SingleOrDefault(g => g.Kapsam == GanoKapsami.AnaDal);
            var programKayit = existing.SingleOrDefault(g => g.Kapsam == kapsam);
            if (!SurumUyar(anaKayit, model.AnaDalSurum) || !SurumUyar(programKayit, model.ProgramSurum)) return GanoKayitSonucu.Cakisma;
            if (reason is null && (anaKayit is not null && (anaDal != anaKayit.Gano || source is not null && source != anaKayit.Kaynak) ||
                programKayit is not null && programGano != programKayit.Gano)) return GanoKayitSonucu.Gecersiz;
            if (anaDal is not null && source is null) return GanoKayitSonucu.Gecersiz;
            // Bosaltma silme degildir; mevcut GANO kaydi/gecmisi korunur.
            if ((anaKayit is not null && anaDal is null) || (programKayit is not null && programGano is null)) return GanoKayitSonucu.Gecersiz;
            var now = (clock ?? TimeProvider.System).GetUtcNow();
            var changed = new Dictionary<Guid, decimal>();
            if (anaKayit is not null && anaDal is decimal a && a != anaKayit.Gano) changed.Add(anaKayit.Id, a);
            if (programKayit is not null && programGano is decimal p && p != programKayit.Gano) changed.Add(programKayit.Id, p);
            var priorPeriods = await (from g in db.GanoKontrolKayitlari.AsNoTracking() join d in db.Donemler.AsNoTracking() on g.DonemId equals d.Id
                where g.ProgramId == model.ProgramId select d.AkademikYilBaslangici * 2 + (int)d.Yariyil - 1).ToListAsync(ct);
            var targetPeriod = akademikYil * 2 + (int)yariyil - 1;
            var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(3)).DateTime);
            // Eski yıl yalnız tarihe bakılarak güncel dönem sayılmaz.
            var notHistorical = IlkDegerlendirmeDonemiMi(yil,yariyil,today,donem);
            // Yeni kayıt yalnız güncel dönemden veya sıradaki dönemden başlar.
            var newPeriod = notHistorical && (priorPeriods.Count == 0 || targetPeriod > priorPeriods.Max());
            var samePeriodKnown = notHistorical && donem is not null && await (
                from s in db.GanoKontrolSonuclari.AsNoTracking() join g in db.GanoKontrolKayitlari.AsNoTracking() on s.GanoKaydiId equals g.Id
                join k in db.KosulKumeleri.AsNoTracking() on s.KosulKumesiId equals k.Id
                join v in db.AkademikKosulSurumleri.AsNoTracking() on k.AkademikKosulSurumuId equals v.Id
                where g.ProgramId == model.ProgramId && g.DonemId == donem.Id && s.RevizyonNo >= 1 &&
                    s.GanoKopyasi == g.Gano && k.Kapsam == g.Kapsam && v.TamamlandiMi && v.ProgramTuru == program.Tur
                select s.Id).AnyAsync(ct);
            var canEvaluateNewScope = newPeriod || samePeriodKnown;
            // Historical entries are allowed even when other periods have evaluations.
            // Without an original rule they remain explicitly unassessed, not today's pass/fail.
            var revisions = await GanoYenidenHesapHazirligi.HazirlaAsync(db, program, changed, actorId, now, ct);
            if (revisions is null) return GanoKayitSonucu.DegerlendirmeTutarsiz;
            if (donem is null)
            {
                donem = new Donem { Id = Guid.NewGuid(), AkademikYilBaslangici = akademikYil, Yariyil = yariyil,
                    Ad = model.Donem, Kod = $"{akademikYil}-{(int)yariyil}" };
                db.Donemler.Add(donem);
                Gecmis(nameof(Donem), donem.Id.ToString(), "DonemEkle", null,
                    new { donem.AkademikYilBaslangici, donem.Yariyil, donem.Ad, donem.Kod }, actorId, now);
            }
            var mainSaved = DegeriKaydet(anaKayit, anaDal, GanoKapsami.AnaDal, model.ProgramId, donem.Id, actorId, now, source, reason);
            var programSaved = DegeriKaydet(programKayit, programGano, kapsam, model.ProgramId, donem.Id, actorId, now, null, reason);
            var added = new List<GanoKontrolKaydi>();
            if (canEvaluateNewScope && anaKayit is null && mainSaved is not null) added.Add(mainSaved);
            if (canEvaluateNewScope && programKayit is null && programSaved is not null) added.Add(programSaved);
            var initial = await GanoIlkDegerlendirme.HazirlaAsync(db,program,added,targetPeriod,actorId,now,ct);
            if (initial is null) return GanoKayitSonucu.DegerlendirmeTutarsiz;
            var results = revisions.Concat(initial).ToArray();
            // Kaynak kopyası her sonucun kendi dönem kaydından gelir; sonraki döneme önceki kaynak taşınmaz.
            if (results.Length > 0)
            {
                var sources = await db.GanoKontrolKayitlari.AsNoTracking().Where(g => g.ProgramId == model.ProgramId).ToDictionaryAsync(g => g.Id, g => g.Kaynak, ct);
                foreach (var saved in new[] { mainSaved, programSaved }) if (saved is not null) sources[saved.Id] = saved.Kaynak;
                foreach (var revision in results) revision.KaynakKopyasi = sources.GetValueOrDefault(revision.GanoKaydiId);
            }
            db.GanoKontrolSonuclari.AddRange(results);
            foreach (var revision in results)
                Gecmis(nameof(GanoKontrolSonucu), revision.Id.ToString(), "GanoSonucRevizyon", null,
                    new { revision.GanoKaydiId, revision.KosulKumesiId, revision.KuralKodu, revision.RevizyonNo, revision.GanoKopyasi, revision.KaynakKopyasi }, actorId, now);
            await db.SaveChangesAsync(ct);
            // Kaydetme anında değerlendirilmemiş geçmişi tamamla; bu sonuç geçmişte verilmiş karar değildir.
            // Ardından tüm dönem zinciri yeniden hesaplanır; tek düşüş hakkı sonraki dönemlere de yansır.
            if (!await DegerlendirmeleriTamamlaAsync(program, actorId, now, ct)) return GanoKayitSonucu.DegerlendirmeTutarsiz;
            return GanoKayitSonucu.Basarili;
        }, ct);
    }

    private async Task<bool> DegerlendirmeleriTamamlaAsync(OgrenciProgrami program, Guid actor,
        DateTimeOffset now, CancellationToken ct)
    {
        var records = await (from g in db.GanoKontrolKayitlari.AsNoTracking()
            join d in db.Donemler.AsNoTracking() on g.DonemId equals d.Id
            where g.ProgramId == program.Id
            orderby d.AkademikYilBaslangici, d.Yariyil
            select new { Record = g, Period = d.AkademikYilBaslangici * 2 + (int)d.Yariyil - 1 }).ToListAsync(ct);
        var recordIds = records.Select(r => r.Record.Id).ToArray();
        var evaluated = await db.GanoKontrolSonuclari.AsNoTracking()
            .Where(s => recordIds.Contains(s.GanoKaydiId))
            .Select(s => s.GanoKaydiId).Distinct().ToListAsync(ct);
        var completedAny = false;
        foreach (var period in records.Where(r => !evaluated.Contains(r.Record.Id)).GroupBy(r => r.Period))
        {
            var initial = await GanoIlkDegerlendirme.HazirlaAsync(db, program,
                period.Select(r => r.Record).ToArray(), period.Key, actor, now, ct);
            if (initial is null) return false;
            foreach (var result in initial)
            {
                result.KaynakKopyasi = records.Single(r => r.Record.Id == result.GanoKaydiId).Record.Kaynak;
                Gecmis(nameof(GanoKontrolSonucu), result.Id.ToString(), "GanoIlkGuncelDegerlendirme", null,
                    new { result.GanoKaydiId, result.KosulKumesiId, result.GanoKopyasi, result.RevizyonNo,
                        Aciklama = "Kaydetme anındaki ilk değerlendirme; geçmişte verilmiş karar değildir." }, actor, now);
            }
            db.GanoKontrolSonuclari.AddRange(initial);
            await db.SaveChangesAsync(ct);
            completedAny |= initial.Count > 0;
        }
        if (!completedAny || evaluated.Count == 0) return true;
        var revised = await GanoYenidenHesapHazirligi.HazirlaAsync(db, program,
            records.ToDictionary(r => r.Record.Id, r => r.Record.Gano), actor, now, ct);
        if (revised is null) return false;
        foreach (var result in revised)
        {
            result.KaynakKopyasi = records.Single(r => r.Record.Id == result.GanoKaydiId).Record.Kaynak;
            Gecmis(nameof(GanoKontrolSonucu), result.Id.ToString(), "GanoSonucRevizyon", null,
                new { result.GanoKaydiId, result.KosulKumesiId, result.GanoKopyasi, result.RevizyonNo }, actor, now);
        }
        db.GanoKontrolSonuclari.AddRange(revised);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool SurumUyar(GanoKontrolKaydi? row, string? version) => SurumUyar(row?.RowVersion, version);
    private static bool SurumUyar(byte[]? rowVersion, string? version)
    {
        if (rowVersion is null) return string.IsNullOrEmpty(version);
        try { var bytes = Convert.FromBase64String(version ?? ""); return bytes.Length == 8 && rowVersion.SequenceEqual(bytes); }
        catch (FormatException) { return false; }
    }

    private GanoKontrolKaydi? DegeriKaydet(GanoKontrolKaydi? row, decimal? value, GanoKapsami scope, Guid programId, Guid periodId, Guid actorId, DateTimeOffset now, string? kaynak, string? gerekce)
    {
        var nextSource = scope == GanoKapsami.AnaDal ? kaynak ?? row?.Kaynak : row?.Kaynak;
        if (value is null || (row?.Gano == value && row?.Kaynak == nextSource)) return row;
        var before = row is null ? null : new { row.Gano, row.Kaynak, row.GirenKullaniciId, row.DuzeltenKullaniciId, row.DegisiklikAni };
        if (row is null)
        {
            row = new() { Id = Guid.NewGuid(), ProgramId = programId, DonemId = periodId, Kapsam = scope, GirenKullaniciId = actorId };
            db.GanoKontrolKayitlari.Add(row);
        }
        else row.DuzeltenKullaniciId = actorId;
        row.Gano = value.Value; row.Kaynak = nextSource; row.DegisiklikAni = now;
        Gecmis(nameof(GanoKontrolKaydi), row.Id.ToString(), before is null ? "GanoEkle" : "GanoDuzenle", before,
            new { row.ProgramId, row.DonemId, row.Kapsam, row.Gano, row.Kaynak, row.GirenKullaniciId, row.DuzeltenKullaniciId, row.DegisiklikAni, Gerekce = gerekce }, actorId, now);
        return row;
    }

    private void Gecmis(string type, string key, string action, object? before, object after, Guid actor, DateTimeOffset now) =>
        db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = type, VarlikAnahtari = key, Islem = action,
            OncekiDeger = before is null ? null : JsonSerializer.Serialize(before), YeniDeger = JsonSerializer.Serialize(after), YapanKullaniciId = actor, IslemAni = now });

    private async Task<bool> YetkiliMiAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return false;
        await db.Entry(actor).ReloadAsync(ct);
        var permission = await access.OkuAsync(actor);
        return permission.Izinli && !permission.ParolaDegisikligiGerekli && permission.Roller.Contains(UygulamaRolleri.Koordinator);
    }

    private async Task<GanoKayitSonucu> IslemAsync(Func<Task<GanoKayitSonucu>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return GanoKayitSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                var result = await operation();
                if (result != GanoKayitSonucu.Basarili) return result;
                if (own is not null) await tx.CommitAsync(ct);
                completed = true;
                return result;
            }
            finally
            {
                if (!completed)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    db.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return GanoKayitSonucu.Cakisma; }
        catch (DbUpdateException) { return GanoKayitSonucu.Cakisma; }
        catch (Exception) { logger.LogWarning("GANO/dönem işlemi tamamlanamadı."); return GanoKayitSonucu.Basarisiz; }
    }
}
