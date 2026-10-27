using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Hesap;
using CapYandal.Web.Services.Paneller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator), AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PersonelHesapController(UserManager<UygulamaKullanicisi> users, PanelModelServisi panels,
    PersonelHesapPanelServisi reader, PersonelErisimServisi accounts, PersonelGorevAtamaServisi assignments) : Controller
{
    // Hesap yönetimi: listeleme ve form yanıtları ortak panel görünümünü kullanır.
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        var entries = await reader.OkuAsync(actor.Id, ct);
        if (entries is null) return Forbid();
        var model = await panels.HazirlaAsync(User, "Koordinatör", PanelBolumleri.Koordinator, ct: ct);
        if (model is null) return Challenge();
        ViewData["PanelBasligi"] = "Koordinatör";
        return View(model with { AktifBolum = "Hesap Yönetimi", PersonelGorevlileri = entries });
    }

    // Hesap hazırlığı: rol verilmeden kurumsal doğrulama daveti oluşturulur.
    [HttpPost, RequestSizeLimit(8192)]
    public async Task<IActionResult> Hazirla(PersonelHesapModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Ad, soyad, kullanıcı adı ve kurumsal adresi kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        var result = await accounts.HazirlaAsync(actor.Id, model, ct);
        if (result.Durum == PersonelHesapSonucu.Hazirlandi)
        {
            var delivery = await accounts.DavetEtAsync(actor.Id, result.Id!.Value, ct);
            return Ok(new { kaydedildi = true, bilgi = delivery == PersonelHesapSonucu.Gonderildi
                ? "Hesap hazırlandı ve davet gönderildi. Parola kurulumundan sonra görev verebilirsiniz."
                : "Hesap hazırlandı; e-posta gönderilemedi. Görev verilmedi; daveti daha sonra yeniden gönderin." });
        }
        return result.Durum switch
        {
            PersonelHesapSonucu.IzinYok => StatusCode(403, new { hata = "Hesap hazırlama yetkiniz yok." }),
            PersonelHesapSonucu.Cakisma => Conflict(new { hata = "Kullanıcı adı veya kurumsal adres zaten kullanılıyor." }),
            PersonelHesapSonucu.UygunDegil => BadRequest(new { hata = "Personel bilgilerini ve görevi kontrol edin." }),
            _ => StatusCode(503, new { hata = "Hesap hazırlanamadı; yeniden göndermeden güncel durumu kontrol edin." })
        };
    }

    [HttpPost, RequestSizeLimit(8192)]
    public async Task<IActionResult> Davet(Guid id, CancellationToken ct)
    {
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        var entries = await reader.OkuAsync(actor.Id, ct);
        if (entries is null) return Forbid();
        if (id == Guid.Empty || !entries.Any(x => x.Id == id && !x.GorevliMi)) return NotFound();
        var result = await accounts.DavetEtAsync(actor.Id, id, ct);
        return result == PersonelHesapSonucu.Gonderildi
            ? Ok(new { kaydedildi = true, bilgi = "Davet gönderildi; görev henüz verilmedi." })
            : StatusCode(503, new { hata = "Davet gönderilemedi; görev verilmedi. Güncel durumu kontrol edin." });
    }

    // İlk görev verilirken hazırlık hesabı da etkinleştirilir.
    [HttpPost, RequestSizeLimit(8192)]
    public async Task<IActionResult> GorevAta(PersonelGorevAtaModeli model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Hazırlanan hesabı ve görevi kontrol edin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        return await assignments.IlkGoreviAtaAsync(actor.Id, model, ct) switch
        {
            PersonelGorevAtamaSonucu.Basarili => Ok(new { kaydedildi = true, bilgi = "Görev devredildi; önceki görevlinin erişimi kapatıldı. Kendi görevinizi devrettiyseniz yeni görevli giriş yapmalıdır." }),
            PersonelGorevAtamaSonucu.IzinYok => StatusCode(403, new { hata = "Bu hesaba görev verme yetkiniz yok." }),
            PersonelGorevAtamaSonucu.Gecersiz => BadRequest(new { hata = "Hazırlanan hesabı ve görevi kontrol edin." }),
            PersonelGorevAtamaSonucu.Cakisma => Conflict(new { hata = "Hesap durumu değişti veya parola kurulumu tamamlanmadı. Sayfayı yenileyin." }),
            _ => StatusCode(503, new { hata = "Görev sonucu doğrulanamadı; yeniden göndermeden sayfayı yenileyin." })
        };
    }
}
