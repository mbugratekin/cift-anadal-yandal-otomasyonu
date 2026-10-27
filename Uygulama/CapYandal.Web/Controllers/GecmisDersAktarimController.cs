using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

// İlk form: yetki serviste tekrar doğrulanır; belirsiz gönderim otomatik yinelenmez.
[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GecmisDersAktarimController(GecmisDersAktarimServisi service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Secenekler(Guid programId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.SeceneklerAsync(actor, programId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(262144)]
    public async Task<IActionResult> Hazirla(Guid programId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Program bilgisini kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        return await service.DonemleriHazirlaAsync(actor, programId, ct) switch
        {
            DersDenemeSonucu.Basarili => Ok(),
            DersDenemeSonucu.IzinYok => Forbid(),
            DersDenemeSonucu.Bulunamadi => NotFound(),
            DersDenemeSonucu.Gecersiz => BadRequest("Öğrencinin programa kabul yılı/dönemi ile işlem dönemini kontrol edin."),
            DersDenemeSonucu.Cakisma => Conflict("Dönem bilgileri değişti. Sayfayı yenileyin."),
            _ => StatusCode(503, "Dönem seçenekleri hazırlanamadı. Otomatik tekrar yapılmadı; sayfayı yenileyin.")
        };
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(262144)]
    public async Task<IActionResult> Kaydet(GecmisDersAktarimModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Dersleri, dönemi ve dayanak gerekçesini kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.AktarAsync(actor, model, ct);
        return result.Sonuc switch
        {
            DersDenemeSonucu.Basarili => Ok(),
            DersDenemeSonucu.IzinYok => Forbid(),
            DersDenemeSonucu.Bulunamadi => NotFound(),
            DersDenemeSonucu.Gecersiz => BadRequest("Katalog, not, dönem ve dayanak bilgilerini kontrol edin."),
            DersDenemeSonucu.Cakisma => Conflict("Bu dönemde zaten liste var veya bilgiler değişti. Mevcut kayıtlar değiştirilmedi; sayfayı yenileyin."),
            _ => StatusCode(503, "Aktarım sonucu doğrulanamadı. Tekrar göndermeden önce dönem kayıtlarını kontrol edin.")
        };
    }
}
