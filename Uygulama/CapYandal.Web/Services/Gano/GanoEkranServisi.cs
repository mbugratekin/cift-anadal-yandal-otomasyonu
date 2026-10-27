using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Gano;

// Ekranda yalnız daha önce kaydedilmiş GANO sonuçlarını gösterir.
public sealed class GanoEkranServisi(AppDbContext db, GanoSonucArsivServisi archive)
{
    public async Task<IReadOnlyList<GanoGorunumSatiri>?> OkuAsync(Guid actorId, Guid programId, CancellationToken ct = default)
    {
        var read = await archive.OkuAsync(actorId, programId, ct);
        if (read.Durum != GanoArsivDurumu.Basarili) return null;
        var records = await (from g in db.GanoKontrolKayitlari.AsNoTracking()
            join d in db.Donemler.AsNoTracking() on g.DonemId equals d.Id
            where g.ProgramId == programId
            orderby d.AkademikYilBaslangici, d.Yariyil, g.Kapsam
            select new { g.Id, g.Kapsam, g.Gano, d.AkademikYilBaslangici, d.Yariyil }).ToListAsync(ct);
        var sets = read.Zincirler.Select(z => z.Guncel.KosulKumesiId).Distinct().ToArray();
        var rules = await db.KosulDegerleri.AsNoTracking().Where(r => sets.Contains(r.KosulKumesiId))
            .Select(r => new { r.KosulKumesiId, r.KuralKodu, r.Amac, r.KontrolTuru, r.Aciklama }).ToListAsync(ct);
        return records.Select(g => {
            var candidates = read.Zincirler.Where(z => z.Guncel.GanoKaydiId == g.Id && rules.Any(r =>
                r.KosulKumesiId == z.Guncel.KosulKumesiId && r.KuralKodu == z.Guncel.KuralKodu &&
                r.Amac == GanoKontrolAmaci.Devam)).Select(z => z.Guncel).ToArray();
            var result = candidates.Length == 1 ? candidates[0] : null;
            var matches = result is not null && result.GanoKopyasi == g.Gano;
            var colour = matches ? result!.Renk : null;
            // Teknik yeniden hesaplama durumu kullanıcıya hata gibi gösterilmez.
            var status = !matches ? "—" : AciklamaUret(result!);
            if (matches && result!.GecmisEksikMi && result.Kod != GanoSonucKodu.BelirsizGecmisEksik)
                status += "; geçmiş eksik";
            return new GanoGorunumSatiri(g.AkademikYilBaslangici, g.Yariyil == AkademikYariyil.Guz ? "Güz" : "Bahar",
                g.Kapsam, g.Gano, colour, status);
        }).ToArray();
    }

    private static string AciklamaUret(GanoArsivSonucu result)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var normalStr = result.NormalEsik?.ToString("0.##", tr) ?? "2,50";
        var altStr = result.AltEsik?.ToString("0.##", tr);
        var gano = result.GanoKopyasi;

        return result.Kod switch
        {
            GanoSonucKodu.Normal => "Devam koşulu sağlanıyor",
            GanoSonucKodu.Istisna =>
                $"{result.DusukDonemSayisi ?? 1} dönem {normalStr} altında kaldığı için uyarı (1 defalık hak)",
            GanoSonucKodu.Ihlal =>
                altStr != null && gano.HasValue && gano.Value < result.AltEsik!.Value
                    ? $"{altStr} taban sınırının altında kaldığı için koşul ihlali; koordinatör değerlendirmeli"
                    : result.DusukDonemSayisi is int d && d > 1
                        ? $"{d}. dönem {normalStr} altında kaldığı için koşul ihlali; koordinatör değerlendirmeli"
                        : $"{normalStr} sınırının altında kaldığı için koşul ihlali; koordinatör değerlendirmeli",
            GanoSonucKodu.BelirsizGecmisEksik => "Belirsiz (geçmiş eksik)",
            _ => "Kontrol devre dışı"
        };
    }
}
