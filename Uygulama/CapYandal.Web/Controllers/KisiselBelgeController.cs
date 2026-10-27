using System.Security.Claims;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("kisisel-belgeler")]
public sealed class KisiselBelgeController(KisiselBelgeOkumaServisi reader, BelgeDeposu depot) : Controller
{
    [HttpGet("{id:guid}/indir")]
    public async Task<IActionResult> Indir(Guid id, CancellationToken ct)
        => await Dosya(id,false,ct);
    [HttpGet("{id:guid}/goruntule")]
    public async Task<IActionResult> Goruntule(Guid id,CancellationToken ct) => await Dosya(id,true,ct);
    private async Task<IActionResult> Dosya(Guid id,bool inline,CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        // Yetkisiz veya bulunamayan kimlik aynı cevabı verir sahiplik açıklanmaz.
        var row = await reader.OkuAsync(actor, id, ct); if (row is null) return NotFound();
        try
        {
            var stream = await depot.AcAsync(row.Dosya, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if(inline && row.Dosya.Mime=="application/pdf") {
                Response.Headers["Content-Security-Policy"]="sandbox; default-src 'none'";
                Response.Headers.ContentDisposition="inline";
                return File(stream,row.Dosya.Mime,enableRangeProcessing:false);
            }
            return File(stream, row.Dosya.Mime, CapYandal.Web.Models.SunumGorunumu.BelgeAdi(row.Ad), enableRangeProcessing: false);
        }
        catch (InvalidDataException) { return NotFound(); }
        catch (IOException) { return NotFound(); }
        catch (ArgumentException) { return NotFound(); }
        catch (FormatException) { return NotFound(); }
    }
}
