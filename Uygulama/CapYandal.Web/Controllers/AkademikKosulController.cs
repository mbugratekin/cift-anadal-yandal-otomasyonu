using System.Security.Claims;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Gano;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AkademikKosulController(AkademikKosulServisi service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Oku(OgrenciProgramTuru program, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.OkuAsync(actor, program, ct);
        return result.Durum switch
        {
            AkademikKosulDurumu.Basarili => Json(result),
            AkademikKosulDurumu.IzinYok => Forbid(),
            AkademikKosulDurumu.Gecersiz => BadRequest("Program türünü kontrol edin."),
            _ => Conflict("Geçmiş kural sürümü doğrulanamadı; otomatik uyarlama yapılmadı.")
        };
    }

    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken, RequestSizeLimit(65536)]
    public async Task<IActionResult> Kaydet([FromBody] AkademikKosulGirdisi? model, CancellationToken ct)
    {
        if (model is null || !ModelState.IsValid) return BadRequest("Kural girişlerini kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.KaydetAsync(actor, model, ct);
        return result.Durum switch
        {
            AkademikKosulDurumu.Basarili => Json(new { result.SurumId }),
            AkademikKosulDurumu.IzinYok => Forbid(),
            AkademikKosulDurumu.Gecersiz => BadRequest(result.Hata ?? "Kural girişlerini kontrol edin."),
            AkademikKosulDurumu.Cakisma => Conflict("Kurallar değişti. Güncel sürümü yeniden okuyun."),
            AkademikKosulDurumu.GecmisUyarlamasiGerekli => Conflict("Geçmiş sürüm doğrulanamadı; otomatik uyarlama yapılmadı."),
            _ => StatusCode(503, "Kayıt sonucu doğrulanamadı. Tekrar göndermeden güncel sürümü kontrol edin.")
        };
    }
}
