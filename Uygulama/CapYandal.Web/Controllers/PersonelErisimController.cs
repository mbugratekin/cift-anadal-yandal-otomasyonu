using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Hesap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CapYandal.Web.Controllers;

[AllowAnonymous, ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class PersonelErisimController(PersonelErisimServisi service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Parola(PersonelParolaOnayModeli model,CancellationToken ct)
    {
        ModelState.Remove(nameof(model.YeniParola)); ModelState.Remove(nameof(model.YeniParolaTekrar));
        model.YeniParola = model.YeniParolaTekrar = "";
        return ModelState.IsValid && await service.GecerliMiAsync(model,ct) ? View(model) : View("~/Views/Hesap/SifreSifirlamaGecersiz.cshtml");
    }
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(HesapGuvenligi.GirisSiniri), RequestSizeLimit(8192)]
    public async Task<IActionResult> Onayla(PersonelParolaOnayModeli model,CancellationToken ct)
    {
        if(!await service.GecerliMiAsync(model,ct)) return View("~/Views/Hesap/SifreSifirlamaGecersiz.cshtml");
        if(!ModelState.IsValid) return ParolaHatasi(model);
        var result=await service.OnaylaAsync(model,ct);
        if(result==PersonelHesapSonucu.Gonderildi) {
            TempData.Remove("Basari");
            return RedirectToAction("Giris","Hesap");
        }
        if(result is PersonelHesapSonucu.DevreDisi or PersonelHesapSonucu.Basarisiz or PersonelHesapSonucu.Cakisma) {
            ModelState.AddModelError("","İşlem tamamlanamadı; parola değiştirilmedi. Daha sonra yeniden deneyin.");
            return ParolaHatasi(model);
        }
        if(result == PersonelHesapSonucu.ParolaKosullari) { ModelState.AddModelError("", "Parola koşulları sağlanmadı. Büyük/küçük harf, rakam ve özel karakter kullanın."); return ParolaHatasi(model); }
        return View("~/Views/Hesap/SifreSifirlamaGecersiz.cshtml");
    }
    private IActionResult ParolaHatasi(PersonelParolaOnayModeli model)
    {
        model.YeniParola = model.YeniParolaTekrar = "";
        ModelState.SetModelValue(nameof(model.YeniParola), null, null); ModelState.SetModelValue(nameof(model.YeniParolaTekrar), null, null);
        return View("Parola", model);
    }
}
