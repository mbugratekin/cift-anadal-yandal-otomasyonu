using System.Security.Claims;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GenelBelgeYuklemeController(GenelBelgeKayitServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Yukle(Guid? genelBelgeId, string? tur, string? surum, IFormFile? file, CancellationToken ct, string? protokolAdi = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        if (!Request.HasFormContentType || Request.Form.Files.Count != 1)
            return Sonuc("Bir PDF veya Word (.docx) dosyası seçin.");
        byte[]? token = null;
        if (surum is not null)
        {
            try { token = Convert.FromBase64String(surum); }
            catch (FormatException) { return Sonuc("Belge sürümü geçersiz; sayfayı yenileyin."); }
        }
        using var protocolStream = tur == "Protokoller" && !string.IsNullOrWhiteSpace(protokolAdi) && file is not null ? file.OpenReadStream() : null;
        if (protocolStream is not null)
        {
            var title = protokolAdi!.Trim();
            if (title.Length > 180 || title.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c))) return Sonuc("Program / kaynak bölüm adını kontrol edin.");
            file = new FormFile(protocolStream, 0, file!.Length, file.Name, title + Path.GetExtension(file.FileName)) { Headers = file.Headers, ContentType = file.ContentType };
        }
        var result = await service.KaydetAsync(actor, genelBelgeId, tur, token, file, ct);
        if (result.Kod == GenelBelgeKayitKodu.IzinYok) return Forbid();
        return Sonuc(result.Kod switch
        {
            GenelBelgeKayitKodu.Basarili => "Belge yüklendi. Önceki sürümler korunuyor.",
            GenelBelgeKayitKodu.Cakisma => "Belge başka işlemde değişti; sayfayı yenileyip yeniden seçin.",
            GenelBelgeKayitKodu.Bulunamadi => "Belge bulunamadı; sayfayı yenileyin.",
            GenelBelgeKayitKodu.Gecersiz => "PDF veya Word (.docx) seçin. Dosya boş olmamalı, 10 MB’ı aşmamalı; belge türü ve sürümü geçerli olmalı.",
            _ => "Belge kaydedilemedi. Otomatik tekrar yapılmadı; mevcut belge korunuyor."
        }, result.Kod == GenelBelgeKayitKodu.Basarili);
    }
    private IActionResult Sonuc(string message, bool success = false)
    {
        if(Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return StatusCode(success ? 200 : 400, new { kaydedildi = success, hata = success ? null : message });
        TempData["GenelBelgeSonucu"] = message;
        return RedirectToAction("Dokumanlar", "Panel");
    }
}
