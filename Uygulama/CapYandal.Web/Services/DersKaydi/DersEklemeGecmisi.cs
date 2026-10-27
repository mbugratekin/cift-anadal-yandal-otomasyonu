using System.Text.Json;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;

namespace CapYandal.Web.Services.DersKaydi;

public static class DersEklemeGecmisi
{
    // İlk kopya başlangıç listesidir; sonraki kopyalardaki yeni satırlar değişikliktir.
    public static IReadOnlyList<DersEklemeSatiri> Oku(IEnumerable<DersListeSurumu> revisions)
    {
        var result = new List<DersEklemeSatiri>(); HashSet<Guid>? previous = null;
        foreach (var revision in revisions.OrderBy(x => x.SurumNo))
        {
            using var json = JsonDocument.Parse(revision.KopyaJson);
            var items = json.RootElement.GetProperty("Kalemler").EnumerateArray().ToArray();
            var current = items.Select(x => x.GetProperty("Id").GetGuid()).ToHashSet();
            if (previous is not null && !revision.EskiKayitKopyasi)
                foreach(var item in items.Where(x => !previous.Contains(x.GetProperty("Id").GetGuid())))
                    result.Add(new(item.GetProperty("DersKoduKopyasi").GetString() ?? "—", item.GetProperty("DersAdiKopyasi").GetString() ?? "—", revision.IslemAni, revision.Gerekce));
            previous = current;
        }
        return result;
    }
}
