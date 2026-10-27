using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Gano;

public enum GanoArsivDurumu { Basarili, Gecersiz, IzinYok, Bulunamadi, GecmisTutarsiz }
public sealed record GanoArsivSonucu(Guid Id, Guid GanoKaydiId, Guid DonemId, GanoKapsami Kapsam,
    Guid KosulKumesiId, string KuralKodu, int? RevizyonNo, decimal? GanoKopyasi,
    GanoSonucKodu Kod, GanoSonucRengi? Renk, bool GecmisEksikMi,
    decimal? NormalEsik, decimal? AltEsik, int? IzinSayisi, int? DusukDonemSayisi,
    Guid KontrolEdenKullaniciId, DateTimeOffset KontrolAni)
{
    public string? KaynakKopyasi { get; init; }
}
public sealed record GanoArsivZinciri(GanoArsivSonucu Guncel, IReadOnlyList<GanoArsivSonucu> Gecmis);
public sealed record GanoArsivOkuma(GanoArsivDurumu Durum, IReadOnlyList<GanoArsivZinciri> Zincirler);

// Saklanan GANO sonuçlarını değiştirmeden geçmişiyle birlikte okur.
public sealed class GanoSonucArsivServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users, HesapErisimi access)
{
    public async Task<GanoArsivOkuma> OkuAsync(Guid actorId, Guid programId, CancellationToken ct = default)
    {
        GanoArsivOkuma Empty(GanoArsivDurumu status) => new(status, Array.Empty<GanoArsivZinciri>());
        if (actorId == Guid.Empty || programId == Guid.Empty) return Empty(GanoArsivDurumu.Gecersiz);
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return Empty(GanoArsivDurumu.IzinYok);
        await db.Entry(actor).ReloadAsync(ct);
        var permission = await access.OkuAsync(actor);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli ||
            !permission.Roller.Any(r => r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani))
            return Empty(GanoArsivDurumu.IzinYok);
        var student = permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        var visible = await db.OgrenciProgramlari.AsNoTracking().AnyAsync(p => p.Id == programId &&
            p.HedefBolum == "Bilgisayar Mühendisliği" && (!student || p.OgrenciId == actorId), ct);
        if (!visible) return Empty(GanoArsivDurumu.Bulunamadi);
        // Bütün sürümler tek sorguda okunur.
        var rows = await (from result in db.GanoKontrolSonuclari.AsNoTracking()
            join record in db.GanoKontrolKayitlari.AsNoTracking() on result.GanoKaydiId equals record.Id
            where record.ProgramId == programId
            select new GanoArsivSonucu(result.Id, result.GanoKaydiId, record.DonemId, record.Kapsam,
                result.KosulKumesiId, result.KuralKodu, result.RevizyonNo, result.GanoKopyasi,
                result.SonucKodu, result.Renk, result.GecmisEksikMi, result.KullanilanNormalEsik,
                result.KullanilanAltEsik, result.KullanilanIzinSayisi, result.KullanilanDusukDonemSayisi,
                result.KontrolEdenKullaniciId, result.KontrolAni) { KaynakKopyasi = result.KaynakKopyasi }).ToListAsync(ct);
        var chains = new List<GanoArsivZinciri>();
        foreach (var group in rows.GroupBy(r => (r.GanoKaydiId, r.KuralKodu)))
        {
            var history = group.OrderBy(r => r.RevizyonNo ?? 0).ToArray();
            var latest = history[^1];
            var metadata = history.Select(r => new GanoSonucSurumReferansi(r.Id, r.GanoKaydiId,
                r.KosulKumesiId, r.KuralKodu, r.RevizyonNo, r.GanoKopyasi, r.KontrolAni)).ToArray();
            // Dönem zinciri düzeltme işlemiyle aynı kurallara göre kontrol edilir.
            if (GanoRevizyonKurali.Planla(latest.GanoKaydiId, latest.KuralKodu, latest.Id, metadata, out _) != GanoRevizyonPlanDurumu.Basarili)
                return Empty(GanoArsivDurumu.GecmisTutarsiz);
            chains.Add(new(latest, Array.AsReadOnly(history)));
        }
        return new(GanoArsivDurumu.Basarili, chains.AsReadOnly());
    }
}
