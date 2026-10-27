using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;

namespace CapYandal.Web.Services.DersKaydi;

public sealed record DersIlerlemeGirdisi(Guid KalemId, Guid DersId, string Kod, string Ad, decimal? Kredi, decimal Akts,
    bool ListeOnayli, bool Cikarildi, Guid? DenemeId, KayitOnayDurumu? Karar, DersDenemeDurumu? Durum);

// Kişisel liste özeti — her denemenin son kararı okunur, aynı dersin kredisi yalnız bir kez sayılır.
public static class DersIlerlemeKurali
{
    public static DersIlerlemeModeli Hesapla(IEnumerable<DersIlerlemeGirdisi> entries, int pendingLists)
    {
        static bool Completed(DersIlerlemeGirdisi x) => x.Karar == KayitOnayDurumu.Onaylandi &&
            x.Durum is DersDenemeDurumu.Gecti or DersDenemeDurumu.Muaf;
        var rows = new List<DersIlerlemeSatiri>();
        foreach (var group in entries.GroupBy(x => x.DersId))
        {
            var completed = group.Where(Completed).ToArray();
            var active = group.Where(x => x.ListeOnayli && !x.Cikarildi).ToArray();
            if (completed.Length == 0 && active.Length == 0) continue;
            // Başarılı geçmişin değerleri canlı katalogdan veya sonraki başarısız denemeden değiştirilmez.
            var copies = completed.Length > 0 ? completed : active;
            var credits = copies.Select(x => x.Kredi).Distinct().ToArray();
            var ects = copies.Select(x => x.Akts).Distinct().ToArray();
            var codes = copies.Select(x => x.Kod).Distinct(StringComparer.Ordinal).ToArray();
            var names = copies.Select(x => x.Ad).Distinct(StringComparer.Ordinal).ToArray();
            var conflict = credits.Length > 1 || ects.Length > 1 || codes.Length > 1 || names.Length > 1;
            var first = copies.OrderBy(x => x.KalemId).First();
            rows.Add(new(group.Key, first.Kod, first.Ad, credits.Length == 1 ? credits[0] : null,
                ects.Length == 1 ? ects[0] : null, completed.Length > 0, conflict,
                group.Where(x => x.DenemeId.HasValue).Select(x => x.DenemeId).Distinct().Count()));
        }
        return new(rows.OrderBy(x => x.Kod, StringComparer.Ordinal).ToArray(), pendingLists);
    }
}
