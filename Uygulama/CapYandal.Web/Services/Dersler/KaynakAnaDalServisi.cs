using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Dersler;

public sealed class KaynakAnaDalServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users, HesapErisimi access)
{
    public async Task<int> KaydetAsync(Guid actorId, Guid? id, OgrenciProgramTuru tur, string? ad, bool etkin,
        string? surum, CancellationToken ct)
    {
        ad = ad?.Trim();
        if (id == Guid.Empty || tur is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal) || string.IsNullOrWhiteSpace(ad) || ad.Length > 128) return 400;
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return 409;
        await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tx = own ?? db.Database.CurrentTransaction!;
        var point = "A" + Guid.NewGuid().ToString("N")[..24];
        if (own is null) await tx.CreateSavepointAsync(point, ct);
        var complete = false;
        try
        {
            var actor = await users.FindByIdAsync(actorId.ToString()); if (actor is null) return 403;
            await db.Entry(actor).ReloadAsync(ct); var permission = await access.OkuAsync(actor);
            if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator)) return 403;
            var item = id is null ? null : await db.KaynakAnaDallar.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (id is not null && item is null) return 404;
            if (item is not null && !string.Equals(item.Ad, ad, StringComparison.Ordinal)) return 400;
            if (item is not null && (!string.Equals(Convert.ToBase64String(item.RowVersion), surum, StringComparison.Ordinal) || item.Tur != tur)) return 409;
            if (await db.KaynakAnaDallar.AnyAsync(x => x.Tur == tur && x.Ad == ad && x.Id != id, ct)) return 409;
            var before = item is null ? null : JsonSerializer.Serialize(new { item.Ad, item.Tur, item.EtkinMi });
            // Ad değişikliği geçmiş ana dal adlarını veya atanmış listeleri yeniden yazmaz.
            if (item is null) { item = new() { Id = Guid.NewGuid(), Tur = tur }; db.KaynakAnaDallar.Add(item); }
            item.Ad = ad; item.EtkinMi = etkin;
            // Yeni ÇAP kaynağına boş listeler hazırlanır; mevcut öğrenci listeleri taşınmaz.
            if (tur == OgrenciProgramTuru.Cap && etkin)
            {
                var versions = await db.Mufredatlar.AsNoTracking().ToListAsync(ct);
                var next = (await db.DersSablonlari.Where(t => t.Tur == tur && t.KaynakAnaDal == ad)
                    .MaxAsync(t => (int?)t.MufredatNo, ct) ?? 0) + 1;
                foreach (var version in versions.OrderBy(x => x.BaslangicYili))
                    if (!await db.DersSablonlari.AnyAsync(t => t.MufredatId == version.Id && t.Tur == tur && t.KaynakAnaDal == ad, ct))
                    {
                        db.DersSablonlari.Add(new() { Id = Guid.NewGuid(), MufredatId = version.Id, Tur = tur,
                            HedefBolum = "Bilgisayar Mühendisliği", KaynakAnaDal = ad, Ad = ad,
                            MufredatAdi = version.Ad, MufredatNo = next++, OlusturanKullaniciId = actorId, OlusturulmaAni = DateTimeOffset.UtcNow });
                    }
            }
            db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(KaynakAnaDal), VarlikAnahtari = item.Id.ToString(),
                Islem = "KaynakAnaDalKaydet", YapanKullaniciId = actorId, IslemAni = DateTimeOffset.UtcNow,
                OncekiDeger = before, YeniDeger = JsonSerializer.Serialize(new { item.Ad, item.Tur, item.EtkinMi }) });
            await db.SaveChangesAsync(ct); if (own is not null) await tx.CommitAsync(ct); complete = true; return 200;
        }
        catch (DbUpdateException) { return 409; }
        finally { if (!complete) { if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); } }
    }
}
