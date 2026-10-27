using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Dersler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SablonController(SablonKayitServisi service, UserManager<UygulamaKullanicisi> users) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Ekle(SablonEkleModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.MufredatAdi)) return BadRequest(new { hata = "Müfredat adı zorunlu; program ve bölüm alanlarını kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return YonetimYaniti(await service.EkleAsync(actor.Id, model, ct));
    }

    [HttpPost]
    public async Task<IActionResult> AdDegistir(MufredatAdModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Müfredat adı zorunlu ve en fazla 100 karakter olmalı." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return YonetimYaniti(await service.AdDegistirAsync(actor.Id, model, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Durum(SablonDurumModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Şablon ve durum alanlarını kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return YonetimYaniti(await service.DurumAsync(actor.Id, model, ct));
    }

    private IActionResult YonetimYaniti(SablonKayitSonucu result) => result switch
    {
        SablonKayitSonucu.Basarili => Ok(new { kaydedildi = true }),
        SablonKayitSonucu.Gecersiz => BadRequest(new { hata = "Müfredat numarası pozitif olmalı. ÇAP için bölüm adı girin; yan dalda kaynak bölüm seçilmez." }),
        SablonKayitSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
        SablonKayitSonucu.Bulunamadi => NotFound(new { hata = "Şablon bulunamadı." }),
        SablonKayitSonucu.Cakisma => Conflict(new { hata = "Müfredat değişmiş veya aynı bölüm/program için bu ad ya da numara kullanılıyor. Güncel listeyi kontrol edin." }),
        _ => StatusCode(503, new { hata = "Kaydedilemedi; tekrar kontrol edin." })
    };

    [HttpPost]
    public async Task<IActionResult> Kaydet(SablonKaydetModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Ders alanlarını kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return (await service.KaydetAsync(actor.Id, model, ct)) switch
        {
            SablonKayitSonucu.Basarili => Ok(new { kaydedildi = true }),
            SablonKayitSonucu.Gecersiz => BadRequest(new { hata = "Ders kodu 7 rakam olmalı ve tekrar etmemeli; ders adı zorunludur. Kredi ve AKTS negatif olamaz, en fazla iki ondalık olmalı. Yarıyıl 1–8 arasında olmalı; ders türünü seçin." }),
            SablonKayitSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
            SablonKayitSonucu.Bulunamadi => NotFound(new { hata = "Şablon bulunamadı." }),
            SablonKayitSonucu.Cakisma => Conflict(new { hata = "Şablon değişmiş veya ders kodu çakışıyor. Girişiniz korunuyor; güncel listeyi kontrol edin." }),
            _ => StatusCode(503, new { hata = "Kaydedilemedi; girişiniz korunuyor." })
        };
    }
}
