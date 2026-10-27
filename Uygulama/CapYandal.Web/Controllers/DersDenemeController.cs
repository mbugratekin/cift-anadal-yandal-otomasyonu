using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

// Ders notu ve karar: aynı servis üzerinden aktör/sürüm/onay/transkript denetimi yapılır.
[Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DersDenemeController(DersDenemeServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(DersDenemeModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Ders, not, dönem, sürüm ve gerekçe bilgilerini kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.KaydetAsync(actor, model, ct);
        return result.Sonuc switch
        {
            DersDenemeSonucu.Basarili => Ok(new { result.DenemeId, result.SurumNo }),
            DersDenemeSonucu.IzinYok => Forbid(),
            DersDenemeSonucu.Bulunamadi => NotFound(),
            DersDenemeSonucu.Gecersiz => BadRequest("Not, karar, dönem ve gerekli gerekçeyi kontrol edin."),
            DersDenemeSonucu.TranskriptEksik => BadRequest("Bu dönem için önce geçerli ÇAP/Yan Dal transkriptini yükleyin."),
            DersDenemeSonucu.Cakisma => Conflict("Ders denemesi değişti. Güncel sürümü görmek için sayfayı yenileyin."),
            _ => StatusCode(503, "İşlem sonucu doğrulanamadı. Otomatik tekrar yapılmadı; önce sayfayı yenileyin.")
        };
    }
}
