using CapYandal.Web.Security;
using CapYandal.Web.Models;
using CapYandal.Web.Services.Ogrenci;
using CapYandal.Web.Services.Hesap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CapYandal.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using CapYandal.Web.Services.Paneller;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class OgrenciKayitController(OgrenciKayitServisi service, IlkDavetServisi davet, AppDbContext database,
    PanelModelServisi panelModelleri) : Controller
{
    [HttpPost, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> DavetGonder(string? ogrenciNo, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actor)) return Challenge();
        var result = await davet.GonderAsync(actor, ogrenciNo, cancellationToken);
        if (result == IlkDavetSonucu.IzinYok) return Forbid();
        TempData["OgrenciKayitSonucu"] = DavetMesaji(result);
        return RedirectToAction("Ogrenciler", "Panel");
    }

    private static string DavetMesaji(IlkDavetSonucu result) => result switch
    {
        IlkDavetSonucu.Gonderildi => "İlk parola bağlantısı kayıtlı öğrenci e-postası için e-posta sunucusuna iletildi. Önceki bağlantı geçersizdir.",
        IlkDavetSonucu.DevreDisi => "Öğrenci kaydı korunuyor; e-posta gönderimi kapalı olduğundan davet gönderilmedi.",
        IlkDavetSonucu.UygunDegil => "Bu kayıt ilk parola davetine uygun değil. Parolası olan öğrenci normal şifre sıfırlama işlemini kullanmalıdır.",
        _ => "Öğrenci kaydı korunuyor; davet gönderilemedi. Yeniden göndermeyi deneyebilirsiniz."
    };
    [HttpGet]
    public async Task<IActionResult> Ekle(CancellationToken cancellationToken)
    {
        await MufredatlariHazirla(cancellationToken);
        if (!await PaneliHazirla(cancellationToken)) return Challenge();
        return View(new OgrenciKayitModel());
    }

    private async Task<bool> PaneliHazirla(CancellationToken ct)
    {
        var panel = await panelModelleri.HazirlaAsync(User, "Koordinatör", PanelBolumleri.Koordinator, ct: ct);
        if (panel is null) return false;
        ViewData["PanelModel"] = panel with { AktifBolum = "Öğrenciler" };
        return true;
    }

    private async Task MufredatlariHazirla(CancellationToken ct)
    {
        var templates = await database.DersSablonlari.AsNoTracking()
            .Where(t => t.EtkinMi && t.HedefBolum == "Bilgisayar Mühendisliği" && t.MufredatAdi != null && t.MufredatAdi != "")
            .OrderBy(t => t.MufredatAdi).ToListAsync(ct);
        var versions = await database.Mufredatlar.AsNoTracking().ToListAsync(ct);
        ViewBag.KaynakAnaDallar = await database.KaynakAnaDallar.AsNoTracking().Where(x => x.EtkinMi).OrderBy(x => x.Ad).ToListAsync(ct);
        ViewBag.EnErkenKabulYili = versions
            .Where(v => v.EtkinMi && v.BaslangicYili.HasValue)
            .Select(v => v.BaslangicYili!.Value)
            .DefaultIfEmpty(DateTime.UtcNow.Year)
            .Min();
        var ready = await database.SablonDersleri.AsNoTracking().Where(x => x.EtkinMi).Select(x => x.SablonId).Distinct().ToListAsync(ct);
        ViewBag.Mufredatlar = templates.Where(t => t.MufredatId is null ||
                versions.Any(v => v.Id == t.MufredatId && v.EtkinMi) && ready.Contains(t.Id))
            .Select(t =>
            {
                var version = versions.SingleOrDefault(v => v.Id == t.MufredatId);
                return new MufredatSecenekModel(t.Id, MufredatGorunumu.Ad(version?.Ad ?? t.MufredatAdi!), t.Tur, t.KaynakAnaDal)
                {
                    OrtakMufredatId = version?.Id,
                    BaslangicYili = version?.BaslangicYili,
                    SonrakiBaslangicYili = version is null ? null : versions
                        .Where(v => v.BaslangicYili.HasValue && (version.BaslangicYili == null || v.BaslangicYili > version.BaslangicYili))
                        .Select(v => v.BaslangicYili).Min()
                };
            }).ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Ekle(OgrenciKayitModel model, CancellationToken cancellationToken)
    {
        await MufredatlariHazirla(cancellationToken);
        if (!ModelState.IsValid)
        {
            if (!await PaneliHazirla(cancellationToken)) return Challenge();
            return View(model);
        }
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actor))
            return Challenge();
        var result = await service.EkleAsync(actor, model, cancellationToken);
        if (result == OgrenciKayitSonucu.IzinYok) return Forbid();
        if (result == OgrenciKayitSonucu.Basarili)
        {
            var delivery = await davet.GonderAsync(actor, model.OgrenciNo, cancellationToken);
            TempData["OgrenciKayitSonucu"] = "Öğrenci kaydedildi. " + DavetMesaji(delivery);
            return RedirectToAction(nameof(Ekle));
        }
        ModelState.AddModelError(string.Empty, result == OgrenciKayitSonucu.Cakisma
            ? "Bu öğrenci numarası veya kurumsal e-posta zaten kayıtlı. Mevcut kayıt değiştirilmedi."
            : "Öğrenci kaydedilemedi. Bilgileri kontrol edip tekrar deneyin.");
        if (!await PaneliHazirla(cancellationToken)) return Challenge();
        return View(model);
    }
}
