using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Ogrenci;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ProgramTakipNotlariController(ProgramTakipNotuServisi notes,
    UserManager<UygulamaKullanicisi> users) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(ProgramTakipNotuEkleModeli model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Not metnini kontrol edin (en fazla 2000 karakter)." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return Yanit(await notes.EkleAsync(actor.Id, model, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(ProgramTakipNotuDuzenleModeli model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Not metni, değişiklik gerekçesi veya sürüm bilgisi geçersiz." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return Yanit(await notes.DuzenleAsync(actor.Id, model, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(ProgramTakipNotuSilModeli model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Not, sürüm ve iptal gerekçesini kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        return Yanit(await notes.SilAsync(actor.Id, model, cancellationToken));
    }

    private IActionResult Yanit(ProgramTakipNotuSonucu result) => result switch
    {
        ProgramTakipNotuSonucu.Basarili => Ok(new { not = notes.SonKayit }),
        ProgramTakipNotuSonucu.Gecersiz => BadRequest(new { hata = "Not bilgilerini kontrol edin." }),
        ProgramTakipNotuSonucu.IzinYok => StatusCode(403, new { hata = "Bu işlem için yetkiniz yok." }),
        ProgramTakipNotuSonucu.Bulunamadi => NotFound(new { hata = "Not veya program artık bulunamıyor. Sayfayı yenileyin." }),
        ProgramTakipNotuSonucu.Cakisma => Conflict(new { hata = "Not başka bir işlemde değişti. Yazınızı koruyup sayfayı yenileyin." }),
        _ => StatusCode(503, new { hata = "Not işlemi tamamlanamadı. Yazınızı koruyup tekrar deneyin." })
    };
}
