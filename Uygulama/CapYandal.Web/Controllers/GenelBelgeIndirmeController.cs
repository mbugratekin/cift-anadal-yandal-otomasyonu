using System.Security.Claims;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

// Belge başına yalnız son sürümün indirilmesi.
public sealed record GenelBelgeIndirmeKaydi(Guid GenelBelgeId, int SurumNo,
    BelgeKapsami Kapsam, Guid? ProgramId, Guid? DersSecimiId, Guid? EvrakGonderimiId,
    string Tur, BelgeIcerikDurumu Durum, string? ResmiBaglanti,
    string DosyaAdi, DepolananBelge Dosya);

public interface IGenelBelgeOkumaKaynagi
{
    // Önce en büyük SurumNo seçilir; içerik eksik diye eski sürüme dönülmez.
    Task<GenelBelgeIndirmeKaydi?> SonSurumAsync(Guid genelBelgeId, CancellationToken iptal);
    Task<GenelBelgeIndirmeKaydi?> SurumAsync(Guid genelBelgeId, int surumNo, CancellationToken iptal) => Task.FromResult<GenelBelgeIndirmeKaydi?>(null);
}

public interface IGenelBelgeGuncelErisim
{
    Task<bool> IndirebilirAsync(ClaimsPrincipal kullanici, CancellationToken iptal);
}

[Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("genel-belgeler")]
public sealed class GenelBelgeIndirmeController(IGenelBelgeOkumaKaynagi kaynak,
    IGenelBelgeGuncelErisim erisim, BelgeDeposu depo) : Controller
{
    private static readonly HashSet<string> Turler =
        ["Akademik takvim", "Protokoller", "Ders kayıt rehberi", "Kurallar ve yönergeler", "Ders kayıt formları"];

    [HttpGet("{id:guid}/indir")]
    public async Task<IActionResult> Indir(Guid id, CancellationToken iptal, bool onizle = false, int? surumNo = null)
    {
        if (!await erisim.IndirebilirAsync(User, iptal)) return Forbid();
        if (!ModelState.IsValid || id == Guid.Empty || surumNo is <= 0) return NotFound();
        var kayit = surumNo is { } requested ? await kaynak.SurumAsync(id, requested, iptal) : await kaynak.SonSurumAsync(id, iptal);
        if (kayit is null || kayit.GenelBelgeId != id || kayit.SurumNo <= 0 || surumNo.HasValue && kayit.SurumNo != surumNo ||
            kayit.Kapsam != BelgeKapsami.Genel || kayit.ProgramId is not null ||
            kayit.DersSecimiId is not null || kayit.EvrakGonderimiId is not null ||
            !Turler.Contains(kayit.Tur) || kayit.Durum != BelgeIcerikDurumu.Mevcut ||
            kayit.ResmiBaglanti is not null) return NotFound();
        if (!BelgeSurumKurali.DosyaGirdisiGecerli(kayit.DosyaAdi, kayit.Dosya.Boyut) ||
            kayit.DosyaAdi.Length > 250 || kayit.DosyaAdi.Any(char.IsControl) ||
            kayit.DosyaAdi.IndexOfAny(['/', '\\', ':']) >= 0 ||
            !Path.GetExtension(kayit.DosyaAdi).Equals(Path.GetExtension(kayit.Dosya.Anahtar), StringComparison.OrdinalIgnoreCase))
            return NotFound();
        try
        {
            var stream = await depo.AcAsync(kayit.Dosya, iptal);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (onizle && kayit.Dosya.Mime == "application/pdf")
            {
                Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
                Response.Headers.ContentDisposition = "inline";
                return File(stream, kayit.Dosya.Mime, enableRangeProcessing: false);
            }
            
            return File(stream, kayit.Dosya.Mime, kayit.DosyaAdi, enableRangeProcessing: false);
        }
        catch (InvalidDataException) { return NotFound(); }
        catch (IOException) { return NotFound(); }
        catch (ArgumentException) { return NotFound(); }
        catch (FormatException) { return NotFound(); }
        // Başarısız okumada eski sürüme veya resmî URL'ye yönlenme yok.
    }
}
