namespace CapYandal.Web.Services.Gano;

// Kilitli kayıtlardan gelen GANO sürümlerinin sırasını denetler.
public sealed record GanoSonucSurumReferansi(Guid SonucId, Guid GanoKaydiId,
    Guid KosulKumesiId, string KuralKodu, int? RevizyonNo, decimal? GanoKopyasi,
    DateTimeOffset KontrolAni);

public sealed record GanoDuzeltmeRevizyonPlani(Guid GanoKaydiId, string KuralKodu,
    Guid KullanilacakKosulKumesiId, Guid IlkSonucId, Guid OncekiSonucId,
    int YeniRevizyonNo);

public enum GanoRevizyonPlanDurumu { Basarili, GecersizGirdi, KullanilanSurumBilinmiyor, GecmisTutarsiz, Cakisma }

public static class GanoRevizyonKurali
{
    public static GanoRevizyonPlanDurumu Planla(Guid ganoKaydiId, string? kuralKodu,
        Guid beklenenSonucId, IReadOnlyCollection<GanoSonucSurumReferansi>? gecmis,
        out GanoDuzeltmeRevizyonPlani? plan)
    {
        plan = null;
        if (ganoKaydiId == Guid.Empty || beklenenSonucId == Guid.Empty ||
            string.IsNullOrWhiteSpace(kuralKodu) || kuralKodu.Length > 80 ||
            kuralKodu != kuralKodu.Trim() || gecmis is null)
            return GanoRevizyonPlanDurumu.GecersizGirdi;
        if (gecmis.Count == 0) return GanoRevizyonPlanDurumu.KullanilanSurumBilinmiyor;
        var rows = gecmis.ToArray();
        if (rows.Any(r => r is null || r.SonucId == Guid.Empty ||
                r.GanoKaydiId != ganoKaydiId || r.KosulKumesiId == Guid.Empty ||
                !string.Equals(r.KuralKodu, kuralKodu, StringComparison.Ordinal) ||
                r.RevizyonNo is <= 0 ||
                (r.RevizyonNo.HasValue && r.GanoKopyasi is null) ||
                (r.GanoKopyasi is decimal value && (value is < 0m or > 4m || decimal.Round(value, 2) != value))) ||
            rows.Select(r => r.SonucId).Distinct().Count() != rows.Length ||
            rows.Count(r => r.RevizyonNo is null) > 1)
            return GanoRevizyonPlanDurumu.GecmisTutarsiz;

        // Eski boş GANO değeri sonradan tahmin edilerek doldurulmaz.
        var ordered = rows.OrderBy(r => r.RevizyonNo ?? 0).ToArray();
        var numbered = ordered.Where(r => r.RevizyonNo.HasValue).ToArray();
        for (var i = 0; i < numbered.Length; i++)
            if (numbered[i].RevizyonNo != i + 1) return GanoRevizyonPlanDurumu.GecmisTutarsiz;
        var first = ordered[0];
        if (ordered.Any(r => r.KosulKumesiId != first.KosulKumesiId))
            return GanoRevizyonPlanDurumu.GecmisTutarsiz;
        var latest = ordered[^1];
        if (latest.SonucId != beklenenSonucId) return GanoRevizyonPlanDurumu.Cakisma;
        var current = latest.RevizyonNo ?? 0;
        if (current == int.MaxValue) return GanoRevizyonPlanDurumu.GecmisTutarsiz;
        plan = new(ganoKaydiId, kuralKodu, first.KosulKumesiId,
            first.SonucId, latest.SonucId, current + 1);
        return GanoRevizyonPlanDurumu.Basarili;
    }
}
