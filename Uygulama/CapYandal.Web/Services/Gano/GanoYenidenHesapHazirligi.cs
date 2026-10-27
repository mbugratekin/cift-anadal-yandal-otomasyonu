using System.Data;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Gano;

// Etkilenen bütün dönemler doğrulandıktan sonra GANO sonuçlarını hazırlar.
internal static class GanoYenidenHesapHazirligi
{
    internal static async Task<IReadOnlyList<GanoKontrolSonucu>?> HazirlaAsync(AppDbContext db,
        OgrenciProgrami program, IReadOnlyDictionary<Guid, decimal> changes, Guid actor,
        DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            return null;
        if (changes.Count == 0) return Array.Empty<GanoKontrolSonucu>();
        var records = await (from g in db.GanoKontrolKayitlari.AsNoTracking()
            join d in db.Donemler.AsNoTracking() on g.DonemId equals d.Id
            where g.ProgramId == program.Id
            select new { Record = g, Period = d.AkademikYilBaslangici * 2 + (int)d.Yariyil - 1 }).ToListAsync(ct);
        if (changes.Keys.Any(id => !records.Any(r => r.Record.Id == id))) return null;
        var results = await (from s in db.GanoKontrolSonuclari.AsNoTracking()
            join g in db.GanoKontrolKayitlari.AsNoTracking() on s.GanoKaydiId equals g.Id
            where g.ProgramId == program.Id select s).ToListAsync(ct);
        if (results.Count == 0) return Array.Empty<GanoKontrolSonucu>();
        var setIds = results.Select(s => s.KosulKumesiId).Distinct().ToArray();
        var sets = await db.KosulKumeleri.AsNoTracking().Where(s => setIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var rules = await db.KosulDegerleri.AsNoTracking().Where(r => setIds.Contains(r.KosulKumesiId)).ToListAsync(ct);
        var rootIds = sets.Values.Where(s => s.AkademikKosulSurumuId.HasValue).Select(s => s.AkademikKosulSurumuId!.Value).ToArray();
        var roots = await db.AkademikKosulSurumleri.AsNoTracking().Where(v => rootIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var chains = new List<(GanoKontrolKaydi Record, int Period, KosulDegeri Rule, GanoKontrolSonucu Latest, GanoDuzeltmeRevizyonPlani Plan)>();
        foreach (var chain in results.GroupBy(s => (s.GanoKaydiId, s.KuralKodu)))
        {
            var latest = chain.OrderBy(s => s.RevizyonNo ?? 0).Last();
            var refs = chain.Select(s => new GanoSonucSurumReferansi(s.Id, s.GanoKaydiId, s.KosulKumesiId,
                s.KuralKodu, s.RevizyonNo, s.GanoKopyasi, s.KontrolAni)).ToArray();
            if (GanoRevizyonKurali.Planla(chain.Key.GanoKaydiId, chain.Key.KuralKodu, latest.Id, refs, out var plan)
                != GanoRevizyonPlanDurumu.Basarili || plan is null) return null;
            var rec = records.Single(r => r.Record.Id == latest.GanoKaydiId);
            if (!sets.TryGetValue(plan.KullanilacakKosulKumesiId, out var set) || set.ProgramTuru != program.Tur ||
                set.Kapsam != rec.Record.Kapsam || set.AkademikKosulSurumuId is not Guid rootId ||
                !roots.TryGetValue(rootId, out var root) || !root.TamamlandiMi || root.ProgramTuru != program.Tur) return null;
            var matching = rules.Where(r => r.KosulKumesiId == set.Id && r.KuralKodu == chain.Key.KuralKodu).ToArray();
            if (matching.Length != 1) return null;
            var rule = matching[0];
            // Eski kaydın anlamı yalnız adına bakılarak tahmin edilmez.
            if (rule.KuralKimligi is null || rule.Amac is null || rule.KontrolTuru is null || rule.NormalEsik is null ||
                latest.GanoKopyasi is null || latest.GanoKopyasi != rec.Record.Gano ||
                chain.Any(s => s.KullanilanNormalEsik != rule.NormalEsik || s.KullanilanAltEsik != rule.AltEsik ||
                    s.KullanilanIzinSayisi != rule.IzinSayisi)) return null;
            var input = new AkademikKuralGirdisi(rule.KuralKimligi, set.Kapsam, rule.Amac.Value, rule.KontrolTuru.Value,
                rule.NormalEsik.Value, rule.AltEsik,
                rule.KontrolTuru == GanoKontrolTuru.DusmeHakki ? rule.IzinSayisi : rule.ArdisikDonemSayisi, rule.Aciklama);
            if (AkademikKosulGirdiKurali.Hata(program.Tur, [input]) is not null ||
                rule.DegerlendirmeTuru != KosulDegerlendirmeTuru.Otomatik) return null;
            chains.Add((rec.Record, rec.Period, rule, latest, plan));
        }
        var output = new List<GanoKontrolSonucu>();
        foreach (var changedScope in records.Where(r => changes.ContainsKey(r.Record.Id)).GroupBy(r => r.Record.Kapsam))
        {
            // Kuralı bilinmeyen eski değer nötr kalır ve sonraki dönemlerde eksik veri sayılır.
            var start = changedScope.Min(r => r.Period);
            foreach (var c in chains.Where(c => c.Record.Kapsam == changedScope.Key && c.Period >= start).OrderBy(c => c.Period))
            {
                var value = changes.TryGetValue(c.Record.Id, out var replacement) ? replacement : c.Record.Gano;
                GanoEsikSonucu? result;
                if (c.Rule.KontrolTuru == GanoKontrolTuru.TekEsik)
                    result = GanoEsikKontrolu.TekEsik(value, c.Rule.NormalEsik!.Value, c.Latest.GecmisEksikMi, c.Rule.EtkinMi);
                else if (c.Rule.KontrolTuru == GanoKontrolTuru.DusmeHakki)
                {
                    var prior = chains.Where(p => p.Record.Kapsam == c.Record.Kapsam && p.Rule.Amac == c.Rule.Amac && p.Period < c.Period).ToArray();
                    if (prior.Select(p => p.Period).Distinct().Count() != prior.Length) return null;
                    // Eski 'eksik' bayrağı kalıcı değildir: sonradan girilen dönemler boşluğu kapatabilir.
                    var missing = prior.Any(p => !p.Rule.EtkinMi) ||
                        records.Any(r => r.Record.Kapsam == c.Record.Kapsam && r.Period < c.Period &&
                            !prior.Any(p => p.Record.Id == r.Record.Id));
                    var allPeriods = prior.Select(p => p.Period).Append(c.Period).Distinct().ToArray();
                    if ((long)allPeriods.Max() - allPeriods.Min() + 1 != allPeriods.Length) missing = true;
                    if (program.KabulAkademikYilBaslangici is not int kabulYili || program.KabulYariyili is not { } kabulDonemi ||
                        allPeriods.Min() != kabulYili * 2 + (int)kabulDonemi - 1) missing = true;
                    // Her dönem kendi kayıtlı eşiğiyle yeniden hesaplanır.
                    var low = prior.Count(p => p.Rule.EtkinMi &&
                        (changes.TryGetValue(p.Record.Id, out var corrected) ? corrected : p.Record.Gano) < p.Rule.NormalEsik!.Value);
                    result = GanoEsikKontrolu.CapDevam(value, c.Rule.NormalEsik!.Value, c.Rule.AltEsik,
                        c.Rule.IzinSayisi!.Value, low, missing, c.Rule.EtkinMi);
                }
                else if (c.Rule.KontrolTuru == GanoKontrolTuru.UstUsteDusukDonem)
                {
                    var prior = chains.Where(p => p.Record.Kapsam == c.Record.Kapsam &&
                        p.Rule.Amac == c.Rule.Amac && p.Period < c.Period).ToArray();
                    if (prior.Select(p => p.Period).Distinct().Count() != prior.Length) return null;
                    var missing = c.Latest.GecmisEksikMi || prior.Any(p => p.Latest.GecmisEksikMi);
                    var evidence = prior.Select(p => new GanoDonemKaniti(p.Period, p.Rule.EtkinMi
                        ? (changes.TryGetValue(p.Record.Id, out var corrected) ? corrected : p.Record.Gano) < p.Rule.NormalEsik!.Value
                        : null)).Append(new(c.Period, value < c.Rule.NormalEsik!.Value)).ToArray();
                    var consecutive = ArdisikDonemKaniti.Oku(evidence, c.Period, c.Rule.ArdisikDonemSayisi!.Value);
                    if (!c.Rule.EtkinMi)
                        result = new(GanoSonucKodu.DevreDisi, null, missing, null);
                    else if (value >= c.Rule.NormalEsik.Value)
                        result = new(GanoSonucKodu.Normal, GanoSonucRengi.Yesil, missing, null);
                    else if (consecutive is true)
                        result = new(GanoSonucKodu.Ihlal, GanoSonucRengi.Kirmizi, missing, null);
                    else if (consecutive is null)
                        result = new(GanoSonucKodu.BelirsizGecmisEksik, null, true, null);
                    else
                        result = new(GanoSonucKodu.Istisna, GanoSonucRengi.Sari, missing, null);
                }
                else return null;
                if (result is null) return null;
                output.Add(new() { Id = Guid.NewGuid(), GanoKaydiId = c.Record.Id,
                    KosulKumesiId = c.Plan.KullanilacakKosulKumesiId, KuralKodu = c.Rule.KuralKodu,
                    RevizyonNo = c.Plan.YeniRevizyonNo, GanoKopyasi = value, SonucKodu = result.Kod,
                    Renk = result.Renk, GecmisEksikMi = result.GecmisEksikMi,
                    KullanilanNormalEsik = c.Rule.NormalEsik, KullanilanAltEsik = c.Rule.AltEsik,
                    KullanilanIzinSayisi = c.Rule.IzinSayisi, KullanilanDusukDonemSayisi = result.BilinenDusukDonemSayisi,
                    KontrolEdenKullaniciId = actor, KontrolAni = now });
            }
        }
        return output;
    }
}
