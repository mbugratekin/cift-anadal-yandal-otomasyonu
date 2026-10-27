using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BelgeIncelemeController(BelgeIncelemeServisi service) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Iste(EkBelgeIsteModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Belge isteğini, türünü ve dönemini kontrol edin." });
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        return Cevap(await service.IsteAsync(actor, model, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Karar(BelgeKararModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Karar ve gerekçeyi kontrol edin." });
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        return Cevap(await service.KararVerAsync(actor, model, ct));
    }
    private IActionResult Cevap(BelgeIncelemeSonucu result) => result switch
    {
        BelgeIncelemeSonucu.Basarili => Ok(new { kaydedildi = true }),
        BelgeIncelemeSonucu.Gecersiz => BadRequest(new { hata = "Geçerli bir belge ve karar gerekçesi gerekli." }),
        BelgeIncelemeSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
        BelgeIncelemeSonucu.Bulunamadi => NotFound(new { hata = "Belge veya kayıt bulunamadı." }),
        BelgeIncelemeSonucu.Cakisma => Conflict(new { hata = "Kayıt değişti. Girişlerinizi koruyup sayfayı yenileyin." }),
        _ => StatusCode(503, new { hata = "İşlem kaydedilemedi. Girişleriniz korunuyor." })
    };
}
