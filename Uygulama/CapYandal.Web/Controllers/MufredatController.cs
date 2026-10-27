using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Dersler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles=UygulamaRolleri.Koordinator), ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MufredatController(MufredatYonetimiServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ekle(MufredatEkleModel model, CancellationToken ct) => !ModelState.IsValid ? BadRequest(new { hata="Ad ve başlangıç yılını kontrol edin." }) : await Run(actor=>service.EkleAsync(actor,model,ct));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Durum(MufredatDurumModel model, CancellationToken ct) => !ModelState.IsValid ? BadRequest(new { hata="Müfredat sürümünü kontrol edin." }) : await Run(actor=>service.DurumAsync(actor,model,ct));
    private async Task<IActionResult> Run(Func<Guid,Task<SablonKayitSonucu>> action)
    {
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)) return Challenge();
        return await action(actor) switch {
            SablonKayitSonucu.Basarili=>Ok(new{kaydedildi=true}), SablonKayitSonucu.IzinYok=>Forbid(), SablonKayitSonucu.Bulunamadi=>NotFound(),
            SablonKayitSonucu.Cakisma=>Conflict(new{hata="Aynı ad/başlangıç yılı var veya kayıt değişti. Sayfayı yenileyin."}),
            SablonKayitSonucu.Gecersiz=>BadRequest(new{hata="Ad, başlangıç yılı ve sürümü kontrol edin."}),
            _=>StatusCode(503,new{hata="İşlem doğrulanamadı. Otomatik tekrar yapılmadı; sayfayı yenileyin."}) };
    }
}
