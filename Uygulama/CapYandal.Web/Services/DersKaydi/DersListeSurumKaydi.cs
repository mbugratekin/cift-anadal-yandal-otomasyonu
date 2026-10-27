using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.DersKaydi;

// Ders listesinin o andaki sürüm kopyasını hazırlar.
internal static class DersListeSurumKaydi
{
    public static async Task<DersListeSurumu?> EskiyiKoruAsync(AppDbContext db, DersSecimi list, IEnumerable<DersSecimKalemi> items,
        Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.DersListeSurumleri.AnyAsync(x => x.ListeId == list.Id, ct)) return null;
        return await EkleAsync(db, list, items, actor, now, list.KararGerekcesi, ct, true);
    }

    public static async Task<DersListeSurumu> EkleAsync(AppDbContext db, DersSecimi list, IEnumerable<DersSecimKalemi> items,
        Guid actor, DateTimeOffset now, string? reason, CancellationToken ct, bool oldCopy = false)
    {
        var max = await db.DersListeSurumleri.Where(x => x.ListeId == list.Id).Select(x => (int?)x.SurumNo).MaxAsync(ct) ?? 0;
        foreach(var pending in db.ChangeTracker.Entries<DersListeSurumu>().Where(x => x.State == EntityState.Added && x.Entity.ListeId == list.Id))
            max = Math.Max(max, pending.Entity.SurumNo);
        if (max == int.MaxValue) throw new InvalidOperationException("Liste sürüm sınırına ulaştı.");
        var row = new DersListeSurumu { Id = Guid.NewGuid(), ListeId = list.Id, SurumNo = max + 1, Durum = list.Durum,
            Gerekce = reason, YapanKullaniciId = actor, IslemAni = now, EskiKayitKopyasi = oldCopy,
            KopyaJson = JsonSerializer.Serialize(new { list.Id, list.ProgramId, list.DonemId, list.Durum, list.KararGerekcesi,
                list.OnaylayanKullaniciId, list.OnayAni, list.OgrenciKilidi, list.GecmisAktarimiMi, list.MazeretliMi, list.GonderenKullaniciId, list.GonderimAni,
                Kalemler = items.Where(x => x.IptalAni is null).OrderBy(x => x.Id).ToArray() }) };
        db.DersListeSurumleri.Add(row); return row;
    }
}
