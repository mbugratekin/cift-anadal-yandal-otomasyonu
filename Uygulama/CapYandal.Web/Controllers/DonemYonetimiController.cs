using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Ogrenci;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class DonemYonetimiController(DonemIzniServisi izinler, AppDbContext db,
    UserManager<UygulamaKullanicisi> users, HesapErisimi erisim, TimeProvider saat,
    ILogger<DonemYonetimiController> logger) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Izin(DonemIzniModel model, bool iptal, CancellationToken ct)
    {
        // İptalde yalnız kimlik, sürüm ve gerekçe kullanılır yeni aralık alanları zorunlu değildir.
        if (iptal) foreach (var key in new[] { nameof(model.BaslangicYili), nameof(model.BaslangicYariyili), nameof(model.BitisYili), nameof(model.BitisYariyili) }) ModelState.Remove(key);
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        var status = ModelState.IsValid ? await izinler.KaydetAsync(actor.Id, model, iptal, ct) : 400;
        if (status == 403) return Forbid();
        TempData[status == 200 ? "Basari" : "Hata"] = status switch
        {
            200 => iptal ? "İzin kaydı gerekçesiyle iptal edildi; geçmiş korundu." : "İzin / dondurma kaydedildi.",
            409 => "Dönemler başka bir izinle çakışıyor veya kayıt değişmiş. Sayfayı yenileyin.",
            400 => "Dönem aralığını, kabul dönemini, gerekçeyi ve karar belgesini kontrol edin.",
            _ => "İşlem tamamlanamadı; kayıt durumunu kontrol edin."
        };
        return RedirectToAction("OgrenciAyrintisi", "Panel", new { id = model.ProgramId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ilerlet(Guid id, string? surum, CancellationToken ct, string? secim = null)
    {
        if (id == Guid.Empty || surum is not { Length: 12 } || db.ChangeTracker.HasChanges()) return BadRequest();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var actor = await users.GetUserAsync(User);
            if (actor is null) return Challenge();
            var access = await erisim.OkuAsync(actor);
            if (!access.Izinli || access.ParolaDegisikligiGerekli || !access.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return Forbid();
            var current = await db.Donemler.FromSqlRaw("SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [IslemDonemiMi]=1").SingleOrDefaultAsync(ct);
            if (current is null || current.Id != id || Convert.ToBase64String(current.RowVersion) != surum) return Conflict("İşlem dönemi değişti. Sayfayı yenileyin.");
            if(secim == "mevcut") return RedirectToAction("AkademikKosullar","Panel");
            if(secim is not null && secim != "sonraki") return BadRequest();
            var nextCode = AkademikDonemSirasi.Kod(current.AkademikYilBaslangici, (int)current.Yariyil) + 1;
            if (nextCode > 19997) return BadRequest("Sonraki dönem tanımlanamıyor.");
            var year = nextCode / 2; var semester = (AkademikYariyil)(nextCode % 2 + 1);
            var next = await db.Donemler.SingleOrDefaultAsync(x => x.AkademikYilBaslangici == year && x.Yariyil == semester, ct);
            next ??= new() { Id = Guid.NewGuid(), AkademikYilBaslangici = year, Yariyil = semester, Kod = $"{year}-{(int)semester}", Ad = AkademikDonemSirasi.Ad(nextCode) };
            var before = JsonSerializer.Serialize(new { current.Id, current.AkademikYilBaslangici, current.Yariyil });
            current.IslemDonemiMi = false;
            await db.SaveChangesAsync(ct); // Tek etkin dönem benzersizliğini aynı transaction içinde korur.
            if (db.Entry(next).State == EntityState.Detached) db.Donemler.Add(next);
            next.IslemDonemiMi = true;
            db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(Donem), VarlikAnahtari = next.Id.ToString(),
                YapanKullaniciId = actor.Id, IslemAni = saat.GetUtcNow(), Islem = "IslemDonemiIlerlet",
                OncekiDeger = before, YeniDeger = JsonSerializer.Serialize(new { next.Id, next.AkademikYilBaslangici, next.Yariyil }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            TempData["Basari"] = "İşlem dönemi ilerletildi. Önceki kayıtlar korundu; ders kaydı ayrıca açılır.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) { logger.LogWarning("İşlem dönemi ilerletilemedi: {Tur}", e.GetType().Name); TempData["Hata"] = "Dönem ilerletilemedi; güncel durumu kontrol edin."; }
        return RedirectToAction("AkademikKosullar", "Panel");
    }
}
