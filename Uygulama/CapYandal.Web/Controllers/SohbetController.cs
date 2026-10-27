using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Sohbet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SohbetController(SohbetServisi service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Mesajlar(Guid? ogrenciId, CancellationToken ct, int sayfa = 0)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var data = await service.OkuAsync(actor, ogrenciId, sayfa, ct);
        return data is null ? Forbid() : Json(data.Mesajlar);
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(11534336), RequestFormLimits(MultipartBodyLengthLimit = 11534336)]
    public async Task<IActionResult> Gonder(SohbetGonderModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Mesaj en fazla 2000 karakter olabilir.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        if (Request.Form.Files.Count > 1 || Request.Form.Files.Any(x => x.Name != nameof(model.Ek))) return BadRequest("Tek PDF veya Word eki seçin.");
        var result = await service.GonderAsync(actor, model, ct);
        if (result == SohbetSonucu.Basarili && Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return Ok(new { gonderildi = true });
        return result switch { SohbetSonucu.Basarili => RedirectToAction("Sohbet", "Panel", new { ogrenciId = model.OgrenciId }),
            SohbetSonucu.IzinYok => Forbid(), SohbetSonucu.Gecersiz => BadRequest("Mesaj yazın veya geçerli bir PDF/DOCX ekleyin; ek boş veya 10 MB'tan büyük olamaz."),
            SohbetSonucu.Cakisma => Conflict("İstek daha önce farklı içerikle kullanılmış. Sayfayı yenileyin."),
            _ => StatusCode(503, "Gönderim sonucu doğrulanamadı. Otomatik tekrar yapılmadı; önce mesajları kontrol edin.") };
    }
    [HttpGet]
    public async Task<IActionResult> Bildirimler(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var total = await service.BildirimSayisiAsync(actor, ct);
        return total is null ? Forbid() : Json(new { toplam = total });
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(32768)]
    public async Task<IActionResult> Okundu(SohbetOkunduModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.OkunduAsync(actor, model, ct);
        if (result == SohbetSonucu.Basarili)
        {
            var conversation = await service.OkuAsync(actor, model.OgrenciId, 0, ct);
            if (conversation is null) return Forbid();
            var unread = conversation.Ogrenciler.SingleOrDefault(x => x.Id == model.OgrenciId)?.Okunmamis ?? 0;
            return Json(new { okundu = true, konusmaOkunmamis = unread });
        }
        return result switch { SohbetSonucu.IzinYok => Forbid(), SohbetSonucu.Gecersiz => BadRequest(), _ => StatusCode(503) };
    }
    [HttpGet]
    public async Task<IActionResult> Ek(Guid id, CancellationToken ct, bool onizle = false)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await service.EkOkuAsync(actor, id, ct);
        if (result is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        if (onizle && result.IcerikTuru == "application/pdf")
        {
            Response.Headers.ContentDisposition = "inline";
            return File(result.Stream, result.IcerikTuru, enableRangeProcessing: false);
        }
        return File(result.Stream, result.IcerikTuru, result.DosyaAdi);
    }
}
