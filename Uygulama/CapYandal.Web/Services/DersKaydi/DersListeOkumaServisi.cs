using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.DersKaydi;

public sealed class DersListeOkumaServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users, HesapErisimi access, TimeProvider clock)
{
    // DERS İLERLEMESİ: Yetkili programın liste kalemleri ve son sonuçlarından tamamlanma özeti hazırlanır.
    public async Task<DersIlerlemeModeli?> IlerlemeOkuAsync(Guid actorId, Guid programId, CancellationToken ct = default)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return null;
        await db.Entry(actor).ReloadAsync(ct);
        var permission = await access.OkuAsync(actor);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli) return null;
        var staff = permission.Roller.Contains(UygulamaRolleri.Koordinator) || permission.Roller.Contains(UygulamaRolleri.BolumBaskani);
        var student = permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        var program = await db.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId && x.HedefBolum == "Bilgisayar Mühendisliği", ct);
        if (program is null || (!staff && (!student || program.OgrenciId != actorId || !program.AktifMi))) return null;
        var lists = await db.DersSecimleri.AsNoTracking().Where(x => x.ProgramId == programId).ToDictionaryAsync(x => x.Id, ct);
        var items = await db.DersSecimKalemleri.AsNoTracking().Where(x => x.ProgramId == programId).ToListAsync(ct);
        var attempts = await db.DersDenemeleri.AsNoTracking().Where(x => x.ProgramId == programId).ToDictionaryAsync(x => x.DersSecimKalemiId, ct);
        var ids = attempts.Values.Select(x => x.Id).ToArray();
        var latest = await db.DersDenemeSurumleri.AsNoTracking().Where(x => ids.Contains(x.DenemeId) &&
            !db.DersDenemeSurumleri.Any(n => n.DenemeId == x.DenemeId && n.SurumNo > x.SurumNo)).ToDictionaryAsync(x => x.DenemeId, ct);
        return DersIlerlemeKurali.Hesapla(items.Select(x =>
        {
            var attempt = attempts.GetValueOrDefault(x.Id);
            var grade = attempt is null ? null : latest.GetValueOrDefault(attempt.Id);
            return new DersIlerlemeGirdisi(x.Id, x.DersId, x.DersKoduKopyasi, x.DersAdiKopyasi, x.KrediKopyasi, x.AktsKopyasi,
                lists[x.ListeId].Durum == DersSecimiDurumu.Onaylandi, x.IptalAni.HasValue, attempt?.Id, grade?.OnayDurumu, grade?.Durum);
        }), lists.Values.Count(x => x.Durum == DersSecimiDurumu.OnayBekliyor));
    }

    // DERS HAVUZU VE LİSTE OKUMA: Rol, dönem ve atanmış şablon kapsamında ekran verisi hazırlanır.
    public async Task<DersPanelModel?> OkuAsync(Guid actorId, bool selection, int year, int semester, CancellationToken ct)
    {
        var actor = await users.FindByIdAsync(actorId.ToString()); if (actor is null) return null;
        await db.Entry(actor).ReloadAsync(ct); var permission = await access.OkuAsync(actor);
        var student = permission.Roller.Contains(UygulamaRolleri.Ogrenci); var coordinator = permission.Roller.Contains(UygulamaRolleri.Koordinator);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !(student || coordinator || permission.Roller.Contains(UygulamaRolleri.BolumBaskani)) ||
            (selection && !student)) return null;
        var academicYear = semester == 2 ? year - 1 : year;
        var period = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(p => p.AkademikYilBaslangici == academicYear && (int)p.Yariyil == semester, ct);
        if (period is null) return new(year, semester, false, []);
        var gate = await db.DersKayitKapilari.AsNoTracking().SingleOrDefaultAsync(g => g.DonemId == period.Id, ct);
        var open = gate is not null && KayitKapisiKurali.EsikleriUygula(KayitKapisiServisi.Durum(gate), clock.GetUtcNow()).AcikMi;
        var query = db.OgrenciProgramlari.AsNoTracking().Where(p => p.HedefBolum == "Bilgisayar Mühendisliği");
        if (student) query = query.Where(p => p.OgrenciId == actorId && p.AktifMi);
        if (!selection && student) query = query.Where(p => db.DersSecimleri.Any(l => l.ProgramId == p.Id && l.DonemId == period.Id));
        var programs = await query.OrderBy(p => p.Id).ToListAsync(ct);
        var programIds = programs.Select(p => p.Id).ToArray();
        var allLists = await db.DersSecimleri.AsNoTracking().Where(x => programIds.Contains(x.ProgramId)).ToListAsync(ct);
        var allListIds = allLists.Select(x => x.Id).ToArray();
        var correctionRequests = await db.DersDuzeltmeTalepleri.AsNoTracking().Where(x => allListIds.Contains(x.ListeId) && x.KapatilmaAni == null).ToListAsync(ct);
        var progressItems = await db.DersSecimKalemleri.AsNoTracking().Where(x => programIds.Contains(x.ProgramId)).ToListAsync(ct);
        var progressAttempts = await db.DersDenemeleri.AsNoTracking().Where(x => programIds.Contains(x.ProgramId)).ToListAsync(ct);
        var progressAttemptIds = progressAttempts.Select(x => x.Id).ToArray();
        var latestGrades = await db.DersDenemeSurumleri.AsNoTracking().Where(x => progressAttemptIds.Contains(x.DenemeId) &&
            !db.DersDenemeSurumleri.Any(newer => newer.DenemeId == x.DenemeId && newer.SurumNo > x.SurumNo)).ToDictionaryAsync(x => x.DenemeId, ct);
        var listById = allLists.ToDictionary(x => x.Id);
        var attemptByItem = progressAttempts.ToDictionary(x => x.DersSecimKalemiId);
        var lists = allLists.Where(l => l.DonemId == period.Id).ToList();
        var listIds = lists.Select(l => l.Id).ToArray();
        var listHistory = await db.DersListeSurumleri.AsNoTracking().Where(x => listIds.Contains(x.ListeId)).ToListAsync(ct);
        var items = progressItems.Where(k => listIds.Contains(k.ListeId)).ToList();
        var attempts = progressAttempts.Where(x => x.DonemId == period.Id).ToList();
        var attemptIds = attempts.Select(x => x.Id).ToArray();
        var revisions = await db.DersDenemeSurumleri.AsNoTracking().Where(x => attemptIds.Contains(x.DenemeId)).ToListAsync(ct);
        var passedIds = revisions.Where(x => x.GecilenDonemId.HasValue).Select(x => x.GecilenDonemId!.Value).Distinct().ToArray();
        var passedPeriods = await db.Donemler.AsNoTracking().Where(x => passedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Ad, ct);
        var currentPeriod = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
        var priorPeriod = currentPeriod is not null && (period.AkademikYilBaslangici < currentPeriod.AkademikYilBaslangici ||
            (period.AkademikYilBaslangici == currentPeriod.AkademikYilBaslangici && (int)period.Yariyil < (int)currentPeriod.Yariyil));
        var templateIds = programs.Where(p => p.MufredatSablonId.HasValue).Select(p => p.MufredatSablonId!.Value).Concat(lists.Select(l => l.SablonId)).Distinct().ToArray();
        var templates = await db.DersSablonlari.AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var groupNames = await db.Mufredatlar.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Ad, ct);
        var courses = await db.SablonDersleri.AsNoTracking().Where(c => templateIds.Contains(c.SablonId) && c.EtkinMi).ToListAsync(ct);
        var courseIds = courses.Select(c => c.DersId).Distinct().ToArray();
        var catalog = await db.Dersler.AsNoTracking().Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        // Kapsam eşitliği atama/gönderimdeki gibi DB collation'ında değerlendirilir.
        var matching = await (from p in db.OgrenciProgramlari.AsNoTracking()
            from t in db.DersSablonlari.AsNoTracking()
            where programIds.Contains(p.Id) && templateIds.Contains(t.Id) && t.EtkinMi && t.Tur == p.Tur && t.HedefBolum == p.HedefBolum &&
                ((t.Tur == OgrenciProgramTuru.YanDal && t.KaynakAnaDal == null) || (t.Tur == OgrenciProgramTuru.Cap && t.KaynakAnaDal == p.KaynakAnaDal))
            select new { ProgramId = p.Id, TemplateId = t.Id }).ToListAsync(ct);
        var ownerIds = programs.Select(p => p.OgrenciId).Distinct().ToArray();
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).Select(u => new { u.Id, u.OgrenciNo, u.Ad, u.Soyad }).ToDictionaryAsync(u => u.Id, ct);
        var result = new List<DersPanelListesi>();
        foreach (var program in programs
            .OrderBy(p => owners.GetValueOrDefault(p.OgrenciId)?.OgrenciNo ?? string.Empty)
            .ThenBy(p => owners.GetValueOrDefault(p.OgrenciId)?.Ad ?? string.Empty)
            .ThenBy(p => p.Id))
        {
            var list = lists.SingleOrDefault(l => l.ProgramId == program.Id);
            var templateId = list?.SablonId ?? program.MufredatSablonId;
            var template = templateId.HasValue ? templates.GetValueOrDefault(templateId.Value) : null;
            var eligible = template is not null && matching.Any(m => m.ProgramId == program.Id && m.TemplateId == template.Id);
            var rows = items.Where(k => k.ListeId == list?.Id && k.IptalAni is null).OrderBy(k => k.MufredatYariyiliKopyasi ?? int.MaxValue).ThenBy(k => k.DersKoduKopyasi)
                .Select(k => new DersGorunumSatiri(k.DersId, k.DersKoduKopyasi, k.DersAdiKopyasi, k.KrediKopyasi, k.AktsKopyasi, (int?)k.TurKopyasi, k.MufredatYariyiliKopyasi)).ToList();
            var options = new List<DersGorunumSatiri>();
            if (eligible && (selection || coordinator))
            {
                foreach (var course in courses.Where(c => c.SablonId == template!.Id).OrderBy(c => c.MufredatYariyili).ThenBy(c => c.Kod))
                {
                    var fallback = catalog.GetValueOrDefault(course.DersId);
                    if (fallback is not null) options.Add(new(course.DersId, course.Kod ?? fallback.Kod, course.Ad ?? fallback.Ad,
                        course.Kredi, course.Akts ?? fallback.Akts, (int)course.Tur, course.MufredatYariyili));
                }
            }
            var owner = owners.GetValueOrDefault(program.OgrenciId);
            var grades = new List<DersDenemeSatiri>();
            foreach (var item in items.Where(x => x.ListeId == list?.Id && (x.IptalAni is null || attempts.Any(a => a.DersSecimKalemiId == x.Id))))
            {
                var attempt = attempts.SingleOrDefault(x => x.DersSecimKalemiId == item.Id);
                var history = revisions.Where(x => x.DenemeId == attempt?.Id).OrderByDescending(x => x.SurumNo).ToArray();
                var latest = history.FirstOrDefault();
                var locked = history.Any(x => x.OnayDurumu is KayitOnayDurumu.Onaylandi or KayitOnayDurumu.IptalEdildi);
                var studentEditable = student && priorPeriod && item.IptalAni is null && list?.GecmisAktarimiMi == false && list.Durum == DersSecimiDurumu.Onaylandi &&
                    (latest is null || OnayGecisKurali.OgrenciGonderebilir(latest.OnayDurumu, locked));
                grades.Add(new(item.Id, item.DersKoduKopyasi, item.DersAdiKopyasi, attempt?.Id, latest?.SurumNo ?? 0,
                    latest?.Not, latest?.Durum ?? DersDenemeDurumu.Alindi, latest?.OnayDurumu ?? KayitOnayDurumu.OnayBekliyor,
                    latest?.GecilenDonemId, coordinator || studentEditable,
                    history.Select(x => new DersDenemeGecmisSatiri(x.SurumNo, x.Not, x.Durum, x.OnayDurumu, x.Gerekce, x.IslemAni,
                        x.GecilenDonemId is { } passed ? passedPeriods.GetValueOrDefault(passed) : null) { SayisalNot = x.SayisalNot }).ToList()) { SayisalNot = latest?.SayisalNot });
            }
            var name = owner is null ? "Öğrenci" : $"{owner.OgrenciNo} {owner.Ad} {owner.Soyad}".Trim();
            var progress = DersIlerlemeKurali.Hesapla(progressItems.Where(x => x.ProgramId == program.Id).Select(x =>
            {
                var attempt = attemptByItem.GetValueOrDefault(x.Id);
                var latest = attempt is null ? null : latestGrades.GetValueOrDefault(attempt.Id);
                return new DersIlerlemeGirdisi(x.Id, x.DersId, x.DersKoduKopyasi, x.DersAdiKopyasi, x.KrediKopyasi, x.AktsKopyasi,
                    listById[x.ListeId].Durum == DersSecimiDurumu.Onaylandi, x.IptalAni.HasValue, attempt?.Id, latest?.OnayDurumu, latest?.Durum);
            }), allLists.Count(x => x.ProgramId == program.Id && x.Durum == DersSecimiDurumu.OnayBekliyor));
            IReadOnlyList<DersEklemeSatiri> added = []; string? changeError = null;
            try { added = DersEklemeGecmisi.Oku(listHistory.Where(x => x.ListeId == list?.Id)); }
            catch(Exception ex) when(ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { changeError = "Ders değişikliği geçmişi doğrulanamadı."; }
            result.Add(new(program.Id, list?.Id, period.Id, name, template?.MufredatId is { } commonId ? groupNames[commonId] : template?.MufredatAdi ?? "Adlandırılmamış müfredat",
                template is null ? null : Convert.ToBase64String(template.RowVersion), list is null ? null : Convert.ToBase64String(list.RowVersion),
                list?.Durum == DersSecimiDurumu.Onaylandi, coordinator && program.AktifMi && eligible && (open ? list is not null && list.Durum is not (DersSecimiDurumu.Onaylandi or DersSecimiDurumu.IptalEdildi) : period.IslemDonemiMi && list?.Durum != DersSecimiDurumu.IptalEdildi) || (selection && open && eligible &&
                    (list is null || list.Durum == DersSecimiDurumu.Reddedildi && !list.OgrenciKilidi)), rows, options,
                items.Where(k => k.ListeId == list?.Id && k.IptalAni != null).OrderBy(k => k.IptalAni)
                    .Select(k => new DersIptalSatiri(k.DersKoduKopyasi, k.DersAdiKopyasi, k.IptalAni!.Value, k.IptalGerekcesi ?? "Eski kayıtta belirtilmemiş")).ToList(), grades, list?.Durum, list?.KararGerekcesi,
                listHistory.Where(x => x.ListeId == list?.Id).OrderByDescending(x => x.SurumNo)
                    .Select(x => new DersListeGecmisSatiri(x.SurumNo, x.Durum, x.Gerekce, x.IslemAni, x.EskiKayitKopyasi)).ToList(), list?.GecmisAktarimiMi ?? false, progress) { ProgramAktifMi = program.AktifMi, MazeretliMi = list?.MazeretliMi ?? false,
                    DuzeltilecekDersler = items.Where(k => correctionRequests.Any(r => r.ListeId == list?.Id && r.KalemId == k.Id)).Select(k => k.DersId).ToArray(),
                    EklenenKalemler = added, DegisiklikGecmisiHatasi = changeError });
        }
        return new(year, semester, open, result);
    }
}
