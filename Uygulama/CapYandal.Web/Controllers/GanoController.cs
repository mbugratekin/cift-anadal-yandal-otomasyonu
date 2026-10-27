using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Ogrenci;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class GanoController(GanoKayitServisi service, UserManager<UygulamaKullanicisi> users) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Kaydet(GanoKaydetModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Dönem ve GANO alanlarını kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return Yanit(await service.KaydetAsync(actor.Id, model, ct));
    }

    private IActionResult Yanit(GanoKayitSonucu result) => result switch
    {
        GanoKayitSonucu.Basarili => Ok(new { kaydedildi = true }),
        GanoKayitSonucu.Gecersiz => BadRequest(new { hata = "Dönemi 2026 Bahar veya 2026 Güz biçiminde girin; GANO 0–4 arasında, en fazla iki ondalık olmalı. Mevcut değeri veya kaynağı düzeltirken gerekçe yazın; eski değeri boşaltarak silemezsiniz." }),
        GanoKayitSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
        GanoKayitSonucu.Bulunamadi => NotFound(new { hata = "Program bulunamadı." }),
        GanoKayitSonucu.Cakisma => Conflict(new { hata = "Bu GANO başka bir işlemle değişti. Güncel satırı yeniden açıp düzeltmenizi kontrol edin." }),
        GanoKayitSonucu.DegerlendirmeTutarsiz => UnprocessableEntity(new { hata = "GANO değerlendirme geçmişi doğrulanamadı. Değişiklik kaydedilmedi; geçmişin incelenmesi gerekiyor." }),
        _ => StatusCode(503, new { hata = "Kayıt tamamlanamadı; girişiniz korunuyor." })
    };
}
