using System.Security.Claims;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CapYandal.Web.Services.DersKaydi;

namespace CapYandal.Web.Services.Paneller;

public static class PanelBolumleri
{
    public static IReadOnlyList<string> Ogrenci { get; } = Array.AsReadOnly(new[]
        { "Ders Kaydı ve Evrak İşlemleri", "Onay Takip", "Sohbet", "Dokümanlar" });
    public static IReadOnlyList<string> Koordinator { get; } = Array.AsReadOnly(new[]
        { "Öğrenciler", "Onay Takip", "Sohbet", "Dokümanlar", "Akademik Koşullar", "Hesap Yönetimi", "İşlem Geçmişi" });
    public static IReadOnlyList<string> Baskan { get; } = Array.AsReadOnly(new[]
        { "Öğrenciler", "Onay Takip", "Dokümanlar", "Akademik Koşullar", "İşlem Geçmişi" });
}

// Ortak panel bilgileri — ekranlar aynı kullanıcı ve program izdüşümünü kullanır.
public sealed class PanelModelServisi(AppDbContext database, UserManager<UygulamaKullanicisi> users, TimeProvider clock)
{
    public async Task<PanelModel?> HazirlaAsync(ClaimsPrincipal principal, string baslik,
        IReadOnlyList<string> bolumler, bool ogrenci = false, CancellationToken ct = default)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return null;
        var name = string.Join(" ", new[] { user.Ad, user.Soyad }.Where(x => !string.IsNullOrWhiteSpace(x)));
        IReadOnlyList<ProgramOzetModel> programs = ogrenci ? await database.OgrenciProgramlari.AsNoTracking()
            .Where(p => p.OgrenciId == user.Id).OrderBy(p => p.BaslangicTarihi)
            .Select(p => new ProgramOzetModel(p.Tur == OgrenciProgramTuru.Cap ? "Çift Anadal" : "Yan Dal", p.KaynakAnaDal))
            .ToListAsync(ct) : [];
        var dersMenuleriGorunur = true;
        if (ogrenci)
        {
            var donemId = await database.Donemler.AsNoTracking().Where(x => x.IslemDonemiMi)
                .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            var kapi = donemId is null ? null : await database.DersKayitKapilari.AsNoTracking()
                .SingleOrDefaultAsync(x => x.DonemId == donemId, ct);
            dersMenuleriGorunur = kapi is not null && KayitKapisiKurali.EsikleriUygula(
                KayitKapisiServisi.Durum(kapi), clock.GetUtcNow()).AcikMi;
        }
        return new PanelModel(baslik, bolumler)
        {
            KullaniciAdi = string.IsNullOrWhiteSpace(name) ? user.UserName ?? "Hesap" : name,
            OgrenciNo = user.OgrenciNo, KurumsalEposta = user.Email,
            KisiselEposta = user.BekleyenKisiselEposta ?? user.KisiselEposta,
            KisiselEpostaDogrulandiMi = user.BekleyenKisiselEposta is null && user.KisiselEpostaOnayliMi,
            FotografVarMi = ogrenci && await database.KullaniciFotograflari.AnyAsync(p => p.KullaniciId == user.Id, ct),
            OgrenciMi = ogrenci, OgrenciDersMenuleriGorunur = dersMenuleriGorunur, Programlar = programs
        };
    }
}
