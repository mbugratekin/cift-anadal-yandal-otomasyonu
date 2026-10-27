using System.Security.Claims;
using CapYandal.Web.Controllers;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Belgeler;

public sealed class GenelBelgeOkumaServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access) : IGenelBelgeOkumaKaynagi, IGenelBelgeGuncelErisim
{
    public async Task<bool> IndirebilirAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return false;
        await db.Entry(user).ReloadAsync(ct);
        var current = await access.OkuAsync(user);
        return current.Izinli && !current.ParolaDegisikligiGerekli &&
            current.Roller.Any(r => r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani);
    }

    public Task<GenelBelgeIndirmeKaydi?> SonSurumAsync(Guid id, CancellationToken ct) => OkuAsync(id, null, ct);
    public Task<GenelBelgeIndirmeKaydi?> SurumAsync(Guid id, int surumNo, CancellationToken ct) => OkuAsync(id, surumNo, ct);

    private async Task<GenelBelgeIndirmeKaydi?> OkuAsync(Guid id, int? surumNo, CancellationToken ct)
    {
        if (id == Guid.Empty) return null;
        // Mevcut içerik filtresi önce uygulanmaz: eksik son sürüm, eski sürüme dönmez.
        var row = await db.Belgeler.AsNoTracking().Where(b => b.GenelBelgeId == id && b.Kapsam == BelgeKapsami.Genel && (surumNo == null || b.SurumNo == surumNo))
            .OrderByDescending(b => b.SurumNo).FirstOrDefaultAsync(ct);
        if (row is null || row.GenelBelgeId is null || row.SurumNo is null || row.DosyaAdi is null ||
            row.DepolamaAnahtari is null || row.IcerikTuru is null || row.BoyutBayt is null || row.Sha256 is null) return null;
        return new(row.GenelBelgeId.Value, row.SurumNo.Value, row.Kapsam, row.ProgramId, row.DersSecimiId,
            row.EvrakGonderimiId, row.Tur, row.IcerikDurumu, row.ResmiBaglanti, row.DosyaAdi,
            new(row.DepolamaAnahtari, row.IcerikTuru, row.BoyutBayt.Value, row.Sha256));
    }
}
