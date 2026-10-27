using System.Data;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Gano;

internal static class GanoIlkDegerlendirme
{
    // İLK GANO DEĞERLENDİRMESİ: Uygun şart ve bilinen geçmiş kullanılır; bilinmeyen kural uydurulmaz.
    internal static async Task<IReadOnlyList<GanoKontrolSonucu>?> HazirlaAsync(AppDbContext db,
        OgrenciProgrami program, IReadOnlyList<GanoKontrolKaydi> added, int period, Guid actor,
        DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return null;
        if (added.Count == 0) return [];
        var root = await db.AkademikKosulSurumleri.AsNoTracking().Where(v => v.ProgramTuru == program.Tur &&
            v.TamamlandiMi && v.OlusturulmaAni <= now).OrderByDescending(v => v.SurumNo).FirstOrDefaultAsync(ct);
        if (root is null) return []; // No default rule is invented.
        var current = await (from r in db.KosulDegerleri.AsNoTracking() join s in db.KosulKumeleri.AsNoTracking() on r.KosulKumesiId equals s.Id
            where s.AkademikKosulSurumuId == root.Id select new { Rule = r, Set = s }).ToListAsync(ct);
        var inputs = current.Select(x => new AkademikKuralGirdisi(x.Rule.KuralKimligi, x.Set.Kapsam,
            x.Rule.Amac ?? 0, x.Rule.KontrolTuru ?? 0, x.Rule.NormalEsik ?? -1, x.Rule.AltEsik,
            x.Rule.KontrolTuru == GanoKontrolTuru.DusmeHakki ? x.Rule.IzinSayisi : x.Rule.ArdisikDonemSayisi, x.Rule.Aciklama)).ToArray();
        if (AkademikKosulGirdiKurali.Hata(program.Tur, inputs) is not null || current.Any(x =>
            x.Set.ProgramTuru != program.Tur || x.Rule.KuralKimligi is null || x.Rule.DegerlendirmeTuru != KosulDegerlendirmeTuru.Otomatik)) return null;
        var prior = await (from g in db.GanoKontrolKayitlari.AsNoTracking() join d in db.Donemler.AsNoTracking() on g.DonemId equals d.Id
            where g.ProgramId == program.Id && d.AkademikYilBaslangici * 2 + (int)d.Yariyil - 1 < period
            select new { Record = g, Period = d.AkademikYilBaslangici * 2 + (int)d.Yariyil - 1 }).ToListAsync(ct);
        var ids = prior.Select(p => p.Record.Id).ToArray();
        var oldResults = await db.GanoKontrolSonuclari.AsNoTracking().Where(s => ids.Contains(s.GanoKaydiId)).ToListAsync(ct);
        var oldSetIds = oldResults.Select(s => s.KosulKumesiId).Distinct().ToArray();
        var oldRules = await (from r in db.KosulDegerleri.AsNoTracking() join s in db.KosulKumeleri.AsNoTracking() on r.KosulKumesiId equals s.Id
            join v in db.AkademikKosulSurumleri.AsNoTracking() on s.AkademikKosulSurumuId equals v.Id
            where oldSetIds.Contains(s.Id) && s.ProgramTuru == program.Tur && v.ProgramTuru == program.Tur && v.TamamlandiMi
            select new { Rule = r, Set = s }).ToListAsync(ct);
        var output = new List<GanoKontrolSonucu>();
        foreach (var record in added)
        foreach (var item in current.Where(x => x.Set.Kapsam == record.Kapsam))
        {
            var rule = item.Rule;
            var points = new List<(int Period, bool? Low, bool Missing)>();
            foreach (var previous in prior.Where(p => p.Record.Kapsam == record.Kapsam))
            {
                var candidates = oldResults.Where(s => s.GanoKaydiId == previous.Record.Id).GroupBy(s => s.KuralKodu)
                    .Select(g => g.OrderBy(s => s.RevizyonNo ?? 0).Last()).Where(s => oldRules.Any(r =>
                        r.Rule.KosulKumesiId == s.KosulKumesiId && r.Rule.KuralKodu == s.KuralKodu &&
                        r.Set.Kapsam == record.Kapsam && r.Rule.Amac == rule.Amac)).ToArray();
                var last = candidates.Length == 1 ? candidates[0] : null;
                var source = last is null ? null : oldRules.SingleOrDefault(r => r.Rule.KosulKumesiId == last.KosulKumesiId && r.Rule.KuralKodu == last.KuralKodu);
                var chain = last is null ? [] : oldResults.Where(s => s.GanoKaydiId == last.GanoKaydiId && s.KuralKodu == last.KuralKodu)
                    .Select(s => new GanoSonucSurumReferansi(s.Id,s.GanoKaydiId,s.KosulKumesiId,s.KuralKodu,s.RevizyonNo,s.GanoKopyasi,s.KontrolAni)).ToArray();
                var known = last is not null && source?.Rule.NormalEsik is >= 0m and <= 4m && source.Rule.EtkinMi &&
                    source.Rule.KuralKimligi is not null && source.Rule.DegerlendirmeTuru == KosulDegerlendirmeTuru.Otomatik &&
                    last.GanoKopyasi == previous.Record.Gano &&
                    last.KullanilanNormalEsik == source.Rule.NormalEsik &&
                    GanoRevizyonKurali.Planla(last.GanoKaydiId,last.KuralKodu,last.Id,chain,out _) == GanoRevizyonPlanDurumu.Basarili;
                points.Add((previous.Period, known ? previous.Record.Gano < source!.Rule.NormalEsik : null,
                    !known || last!.GecmisEksikMi));
            }
            var periods = points.Select(p => p.Period).Append(period).ToArray();
            var firstAcceptedPeriod = program.KabulAkademikYilBaslangici is int acceptanceYear &&
                program.KabulYariyili is { } acceptanceSemester && acceptanceYear * 2 + (int)acceptanceSemester - 1 == period;
            var missing = (points.Count == 0 && !firstAcceptedPeriod) || points.Any(p => p.Missing) || (long)periods.Max() - periods.Min() + 1 != periods.Length;
            GanoEsikSonucu? result;
            if (rule.KontrolTuru == GanoKontrolTuru.TekEsik)
                result = GanoEsikKontrolu.TekEsik(record.Gano,rule.NormalEsik!.Value,missing,rule.EtkinMi);
            else if (rule.KontrolTuru == GanoKontrolTuru.DusmeHakki)
                result = GanoEsikKontrolu.CapDevam(record.Gano,rule.NormalEsik!.Value,rule.AltEsik,rule.IzinSayisi!.Value,
                    points.Count(p => p.Low is true),missing,rule.EtkinMi);
            else
            {
                var evidence = ArdisikDonemKaniti.Oku(points.Select(p => new GanoDonemKaniti(p.Period,p.Low))
                    .Append(new(period,record.Gano < rule.NormalEsik!.Value)).ToArray(),period,rule.ArdisikDonemSayisi!.Value);
                result = !rule.EtkinMi ? new(GanoSonucKodu.DevreDisi,null,missing,null) : record.Gano >= rule.NormalEsik
                    ? new(GanoSonucKodu.Normal,GanoSonucRengi.Yesil,missing,null) : evidence is true
                    ? new(GanoSonucKodu.Ihlal,GanoSonucRengi.Kirmizi,missing,null) : evidence is false
                    ? new(GanoSonucKodu.Istisna,GanoSonucRengi.Sari,missing,null) : new(GanoSonucKodu.BelirsizGecmisEksik,null,true,null);
            }
            if (result is null) return null;
            output.Add(new() { Id = Guid.NewGuid(), GanoKaydiId = record.Id, KosulKumesiId = rule.KosulKumesiId,
                KuralKodu = rule.KuralKodu, RevizyonNo = 1, GanoKopyasi = record.Gano, SonucKodu = result.Kod, Renk = result.Renk,
                GecmisEksikMi = result.GecmisEksikMi, KullanilanNormalEsik = rule.NormalEsik, KullanilanAltEsik = rule.AltEsik,
                KullanilanIzinSayisi = rule.IzinSayisi, KullanilanDusukDonemSayisi = result.BilinenDusukDonemSayisi,
                KontrolEdenKullaniciId = actor, KontrolAni = now });
        }
        return output;
    }
}
