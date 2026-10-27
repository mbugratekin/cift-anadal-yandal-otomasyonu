using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Ogrenci)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ProfilController(AppDbContext database, UserManager<UygulamaKullanicisi> users, ILogger<ProfilController>? logger = null) : Controller
{
    private static readonly SemaphoreSlim FotoIslemKapisi = new(1, 1);
    [HttpGet]
    public async Task<IActionResult> Fotograf(CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var bytes = await database.KullaniciFotograflari.AsNoTracking()
            .Where(photo => photo.KullaniciId == user.Id).Select(photo => photo.JpegIcerik)
            .SingleOrDefaultAsync(cancellationToken);
        if (bytes is null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(bytes, "image/jpeg");
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(ProfilFotografi.DosyaSiniri + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilFotografi.DosyaSiniri + 65536)]
    public async Task<IActionResult> FotografOnayla(IFormFile? fotograf, bool onay, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (await database.KullaniciFotograflari.AnyAsync(photo => photo.KullaniciId == user.Id, cancellationToken))
            return StatusCode(StatusCodes.Status409Conflict, "Fotoğrafınız daha önce onaylandı; değiştirilemez.");
        if (!ModelState.IsValid || !onay || fotograf is null || fotograf.Length is < 1 or > ProfilFotografi.DosyaSiniri)
            return Hata("Fotoğraf seçip kalıcı yükleme onayını işaretleyin. Dosya en fazla 1 MB olabilir.");
        byte[] normalized;
        if (!await FotoIslemKapisi.WaitAsync(0, cancellationToken))
            return StatusCode(StatusCodes.Status429TooManyRequests, "Başka bir fotoğraf işleniyor. Kısa süre sonra tekrar deneyin.");
        try
        {
            using var stream = new MemoryStream();
            await fotograf.CopyToAsync(stream, cancellationToken);
            normalized = ProfilFotografi.Hazirla(stream.ToArray(), fotograf.FileName, fotograf.ContentType);
        }
        catch (ArgumentException exception) { return Hata(exception.Message); }
        finally { FotoIslemKapisi.Release(); }
        var now = DateTimeOffset.UtcNow;
        var photo = new KullaniciFotografi
        {
            KullaniciId = user.Id,
            JpegIcerik = normalized,
            OnayAni = now
        };
        var audit = new DegisiklikGecmisi
        {
            Id = Guid.NewGuid(),
            VarlikTuru = nameof(KullaniciFotografi),
            VarlikAnahtari = user.Id.ToString(),
            Islem = "ProfilFotografiOnayla",
            YapanKullaniciId = user.Id,
            IslemAni = now,
            YeniDeger = JsonSerializer.Serialize(new { photo.KullaniciId, photo.OnayAni, BoyutBayt = normalized.Length, Sha256 = Convert.ToHexString(SHA256.HashData(normalized)) })
        };
        database.KullaniciFotograflari.Add(photo); database.DegisiklikGecmisi.Add(audit);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            database.Entry(photo).State = EntityState.Detached; database.Entry(audit).State = EntityState.Detached;
            return StatusCode(StatusCodes.Status409Conflict, "Fotoğrafınız başka bir istekte onaylandı; değiştirilemez.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException)
        {
            database.Entry(photo).State = EntityState.Detached; database.Entry(audit).State = EntityState.Detached;
            logger?.LogWarning("Fotoğraf/denetim kaydı tamamlanamadı: {Tur}", error.GetType().Name);
            return StatusCode(503, "Fotoğraf kaydı doğrulanamadı. Yeniden yüklemeden önce sayfayı yenileyin.");
        }
        return RedirectToAction("Ogrenci", "Panel");
    }

    private IActionResult Hata(string message)
    {
        TempData["FotografHatasi"] = message;
        return RedirectToAction("Ogrenci", "Panel");
    }
}
