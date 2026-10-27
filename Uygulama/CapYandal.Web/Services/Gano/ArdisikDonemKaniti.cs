namespace CapYandal.Web.Services.Gano;

// DusukMi must be established against that period's own stored rule/version.
// Eksik dönem normal sayılmaz; bu sınıf yalnız dönem sırasını kontrol eder.
public sealed record GanoDonemKaniti(int DonemSirasi, bool? DusukMi);

public static class ArdisikDonemKaniti
{
    public static bool? Oku(IReadOnlyCollection<GanoDonemKaniti> donemler,
        int mevcutDonemSirasi, int gerekenDonemSayisi)
    {
        ArgumentNullException.ThrowIfNull(donemler);
        if (gerekenDonemSayisi < 1) throw new ArgumentOutOfRangeException(nameof(gerekenDonemSayisi));
        if (donemler.Any(d => d is null) || donemler.Select(d => d.DonemSirasi).Distinct().Count() != donemler.Count)
            throw new ArgumentException("Aynı akademik dönem iki kez sayılamaz.", nameof(donemler));
        var byPeriod = donemler.ToDictionary(d => d.DonemSirasi, d => d.DusukMi);
        if (!byPeriod.TryGetValue(mevcutDonemSirasi, out var current) || current is null) return null;
        var first = (long)mevcutDonemSirasi - gerekenDonemSayisi + 1;
        var relevant = donemler.Where(d => d.DonemSirasi >= first && d.DonemSirasi <= mevcutDonemSirasi).ToArray();
        // Aradaki normal dönem ardışıklığı keser.
        // not a potentially huge user-configured period count.
        if (relevant.Any(d => d.DusukMi is false)) return false;
        return relevant.Length < gerekenDonemSayisi || relevant.Any(d => d.DusukMi is null) ? null : true;
    }
}
