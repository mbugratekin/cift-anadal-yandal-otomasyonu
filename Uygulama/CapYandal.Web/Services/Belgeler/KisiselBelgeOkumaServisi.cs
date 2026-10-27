using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Belgeler;
public sealed record KisiselBelgeIndirmeKaydi(string Ad, DepolananBelge Dosya);
public sealed record TranskriptBaglantisi(Guid Id,Guid DonemId,string Donem,KisiselBelgeTuru Tur,bool Pdf);

public sealed class KisiselBelgeOkumaServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users, HesapErisimi access)
{
    public async Task<IReadOnlyList<TranskriptBaglantisi>> TranskriptlerAsync(Guid actorId,Guid programId,CancellationToken ct=default)
    {
        var actor=await users.FindByIdAsync(actorId.ToString());if(actor is null)return [];
        await db.Entry(actor).ReloadAsync(ct);var permission=await access.OkuAsync(actor);
        if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Any(r=>r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani))return [];
        var student=permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        return await (from b in db.Belgeler.AsNoTracking()
            join p in db.OgrenciProgramlari.AsNoTracking() on b.ProgramId equals (Guid?)p.Id
            join d in db.Donemler.AsNoTracking() on b.ArsivDonemId equals (Guid?)d.Id
            where p.Id==programId && p.HedefBolum=="Bilgisayar Mühendisliği" && (!student || p.OgrenciId==actorId) &&
                b.Kapsam==BelgeKapsami.Kisisel && b.KisiselBelgeSerisiId!=null && b.IcerikDurumu==BelgeIcerikDurumu.Mevcut && b.Sha256!=null && b.DepolamaAnahtari!=null &&
                (b.KisiselTur==KisiselBelgeTuru.AnaDalTranskripti || b.KisiselTur==KisiselBelgeTuru.CapYanDalTranskripti) &&
                (b.OnayDurumu==BelgeOnayDurumu.OnayBekliyor || b.OnayDurumu==BelgeOnayDurumu.Onaylandi) &&
                !db.Belgeler.Any(n=>n.KisiselBelgeSerisiId==b.KisiselBelgeSerisiId && n.KisiselSurumNo>b.KisiselSurumNo)
            orderby d.AkademikYilBaslangici descending,d.Yariyil descending,b.YuklemeAni descending
            select new TranskriptBaglantisi(b.Id,d.Id,d.Ad,b.KisiselTur!.Value,b.IcerikTuru=="application/pdf")).ToListAsync(ct);
    }

    public async Task<KisiselBelgeIndirmeKaydi?> OkuAsync(Guid actorId, Guid belgeId, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || belgeId == Guid.Empty) return null;
        var actor = await users.FindByIdAsync(actorId.ToString()); if (actor is null) return null;
        await db.Entry(actor).ReloadAsync(ct); var permission = await access.OkuAsync(actor);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli ||
            !permission.Roller.Any(r => r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani)) return null;
        var student = permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        var row = await (from b in db.Belgeler.AsNoTracking()
            join p in db.OgrenciProgramlari.AsNoTracking() on b.ProgramId equals p.Id
            where b.Id == belgeId && b.Kapsam == BelgeKapsami.Kisisel && b.GenelBelgeId == null && b.SurumNo == null &&
                b.ResmiBaglanti == null && b.IcerikDurumu == BelgeIcerikDurumu.Mevcut && p.HedefBolum == "Bilgisayar Mühendisliği" &&
                (!student || p.OgrenciId == actorId) &&
                ((b.DersSecimiId != null && b.EvrakGonderimiId == null && db.DersSecimleri.Any(l => l.Id == b.DersSecimiId && l.ProgramId == p.Id)) ||
                 (b.DersSecimiId == null && b.EvrakGonderimiId != null && db.EvrakGonderimleri.Any(e => e.Id == b.EvrakGonderimiId && e.ProgramId == p.Id)))
            select b).SingleOrDefaultAsync(ct);
        if (row?.DosyaAdi is not { } name || row.DepolamaAnahtari is not { } key || row.IcerikTuru is not { } mime ||
            row.BoyutBayt is not { } size || row.Sha256 is not { } sha) return null;
        if (!BelgeSurumKurali.DosyaGirdisiGecerli(name, size) || name.Length > 250 || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !Path.GetExtension(name).Equals(Path.GetExtension(key), StringComparison.OrdinalIgnoreCase)) return null;
        return new(name, new(key, mime, size, sha));
    }
}
