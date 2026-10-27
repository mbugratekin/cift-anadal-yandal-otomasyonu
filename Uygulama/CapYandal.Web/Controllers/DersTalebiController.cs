using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Sohbet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

// Talep gönderimi: alıcı öğrenci kimliği oturumdan gelir.
[Authorize(Roles = UygulamaRolleri.Ogrenci)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DersTalebiController(SohbetServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(8192)]
    public async Task<IActionResult> Gonder(DersTalebiModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Ders kodunu, adını ve açıklama uzunluğunu kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.GonderAsync(actor, new() { IstekId = model.IstekId, OgrenciId = actor, Metin = model.MesajMetni() }, ct);
        return result switch
        {
            SohbetSonucu.Basarili => Ok(),
            SohbetSonucu.IzinYok => Forbid(),
            SohbetSonucu.Gecersiz => BadRequest("Ders talebi bilgilerini kontrol edin."),
            SohbetSonucu.Cakisma => Conflict("Bu istek başka içerikle kullanılmış; önce sohbeti kontrol edin."),
            _ => StatusCode(503, "Sonuç doğrulanamadı. Tekrar göndermeden önce sohbeti kontrol edin.")
        };
    }
}
