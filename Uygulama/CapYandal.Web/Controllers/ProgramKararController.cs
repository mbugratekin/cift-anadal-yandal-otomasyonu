using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Ogrenci;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

// Karar formu: yalnız koordinatör POST yapar; CSRF ve model doğrulaması sunucudadır.
[Authorize(Roles = UygulamaRolleri.Koordinator), AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ProgramKararController(ProgramKararServisi service, ProgramKararUyariServisi warnings) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Kaydet(ProgramKararModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Karar, gerekçe ve sürüm bilgilerini kontrol edin." });
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        if (!model.UyariSonrasiTekrar)
        {
            var warning = await warnings.OkuAsync(actorId, model.ProgramId, model.Karar!.Value, ct);
            if (!string.IsNullOrWhiteSpace(warning))
                return Conflict(new { uyari = warning, tekrarGonder = true });
        }
        return (await service.KaydetAsync(actorId, model, ct)) switch
        {
            ProgramKararSonucu.Basarili => Ok(new { mesaj = "Koordinatör kararı kaydedildi; geçmiş kayıtlar korundu." }),
            ProgramKararSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
            ProgramKararSonucu.Bulunamadi => NotFound(new { hata = "Program bulunamadı." }),
            ProgramKararSonucu.Cakisma => Conflict(new { hata = "Kayıt değişti veya bu karar zaten kayıtlı. Gerekçenizi koruyup sayfayı yenileyin." }),
            ProgramKararSonucu.Gecersiz => BadRequest(new { hata = "Karar bu program türüne uygun değil." }),
            _ => StatusCode(503, new { hata = "Karar kaydedilemedi. Girişleriniz korunuyor." })
        };
    }
}
