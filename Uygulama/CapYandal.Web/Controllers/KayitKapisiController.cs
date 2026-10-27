using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;
[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KayitKapisiController(KayitKapisiServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(KayitKapisiModel model, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        if (!ModelState.IsValid) return BadRequest("Geçerli yıl/dönem, tarih ve sürüm bilgisi gönderin.");
        var result = await service.KaydetAsync(actor, model, ct);
        if (result == KayitKapiSonucu.IzinYok) return Forbid();
        if (result == KayitKapiSonucu.Cakisma) return Conflict("Kayıt durumu değişti; sayfayı yenileyin.");
        if (result == KayitKapiSonucu.Gecersiz) return BadRequest("Tarih/saat sırasını ve kayıt durumunu kontrol edin.");
        if (result == KayitKapiSonucu.Basarisiz) return StatusCode(503, "Kayıt ayarı uygulanamadı; otomatik tekrar yapılmadı.");
        return RedirectToAction("AkademikKosullar", "Panel", new { yil = model.Yil, yariyil = model.Yariyil });
    }
}
