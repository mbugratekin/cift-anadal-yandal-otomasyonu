using System.Security.Claims;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DersController(DersListeServisi lists, DersOnayServisi approvals,
    CapYandal.Web.Services.Belgeler.EvrakKayitServisi documents) : Controller
{
    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> MazeretliKaydet(DersListeKomutModel model,
        [FromServices] CapYandal.Web.Data.AppDbContext db, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Dönemi, dersleri ve kurul kararının dayanağını kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        return Cevap((await MazeretliDersKaydi.KaydetAsync(db, lists, approvals, actor, model, ct)).Sonuc, model, false);
    }
    [HttpPost, Authorize(Roles = UygulamaRolleri.Ogrenci), ValidateAntiForgeryToken, RequestSizeLimit(11534336), RequestFormLimits(MultipartBodyLengthLimit=11534336)]
    public async Task<IActionResult> Gonder(DersListeKomutModel model, IFormFile? file, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Geçerli ders ve dönem bilgisi gönderin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        if(Request.Form.Files.Count>1) return BadRequest("Tek PDF veya Word belgesi gönderin.");
        return Cevap((await documents.ListeyleGonderAsync(actor, new(model.ProgramId, model.DonemId, model.SablonSurum, model.Dersler, model.ListeSurum), file, ct)).Sonuc, model, true);
    }
    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzelt(DersListeKomutModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Geçerli liste, ders ve sürüm bilgisi gönderin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        return Cevap((await lists.DuzeltAsync(actor, new(model.ListeId, model.ListeSurum, model.SablonSurum, model.Dersler ?? [], model.Gerekce), ct)).Sonuc, model, false);
    }
    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> DuzeltVeOnayla(DersListeKomutModel model, [FromServices] CapYandal.Web.Data.AppDbContext db,
        [FromServices] ILogger<DersController> logger, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Geçerli liste, ders ve sürüm bilgisi gönderin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        return Cevap((await DersListeBirlesikKarar.KaydetVeOnaylaAsync(db, lists, approvals, actor,
            new(model.ListeId, model.ListeSurum, model.SablonSurum, model.Dersler ?? [], model.Gerekce), logger, ct)).Sonuc, model, false);
    }

    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> Onayla(DersListeKomutModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Geçerli liste ve sürüm bilgisi gönderin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await approvals.OnaylaAsync(actor, new(model.ListeId, model.ListeSurum), ct);
        var mapped = result switch { DersOnaySonucu.Basarili => DersListeSonucu.Basarili, DersOnaySonucu.IzinYok => DersListeSonucu.IzinYok,
            DersOnaySonucu.Bulunamadi => DersListeSonucu.Bulunamadi, DersOnaySonucu.Cakisma => DersListeSonucu.Cakisma,
            DersOnaySonucu.Gecersiz => DersListeSonucu.Gecersiz, _ => DersListeSonucu.Basarisiz };
        return Cevap(mapped, model, false);
    }
    private IActionResult Cevap(DersListeSonucu result, DersListeKomutModel model, bool student) => result switch
    {
        DersListeSonucu.Basarili => BasariliYonlendir(model, student),
        DersListeSonucu.IzinYok => Forbid(), DersListeSonucu.Bulunamadi => NotFound(),
        DersListeSonucu.Cakisma => Conflict("Liste veya müfredat değişti. Sayfayı yenileyip son içeriği kontrol edin."),
        DersListeSonucu.KayitKapali => Conflict("Bu dönem için ders kaydı kapalı."),
        DersListeSonucu.DonemIzinli => Conflict("Bu dönem izinli / dondurulmuş olarak kayıtlı. Ders göndermek için koordinatörle görüşün."),
        DersListeSonucu.Gecersiz => BadRequest("Ders seçimini ve müfredat bilgilerini kontrol edin. Liste boş veya tekrar dersli olamaz."),
        _ => StatusCode(503, "İşlem sonucu doğrulanamadı. Otomatik tekrar yapılmadı; önce sayfayı yenileyin.")
    };

    private IActionResult BasariliYonlendir(DersListeKomutModel model, bool student)
    {
        if (student) TempData["DersGonderildi"] = "Ders listeniz koordinatöre gönderildi.";
        return RedirectToAction(student ? "DersKaydi" : "OnayTakip", "Panel", new { yil = model.Yil, yariyil = model.Yariyil });
    }

    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> Karar(DersListeKomutModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid || model.Karar is null) return BadRequest("Kararı ve gerekli gerekçeyi kontrol edin.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var input = new DersOnayGirdisi(model.ListeId, model.ListeSurum, model.Karar.Value, model.Gerekce,
            GeriAl: model.Karar == CapYandal.Web.Data.Entities.DersSecimiDurumu.OnayBekliyor);
        if (input.GeriAl && model.DuzeltilecekDersler is { Length: > 0 }) return BadRequest("Onayı geri alma, öğrenciye düzeltme isteği göndermez.");
        var result = model.DuzeltilecekDersler is { Length: > 0 } selected
            ? await approvals.DuzeltmeIsteAsync(actor, input, selected, ct)
            : await approvals.KararVerAsync(actor, input, ct);
        return result switch { DersOnaySonucu.Basarili => RedirectToAction("OnayTakip", "Panel", new { yil = model.Yil, yariyil = model.Yariyil }),
            DersOnaySonucu.IzinYok => Forbid(), DersOnaySonucu.Bulunamadi => NotFound(), DersOnaySonucu.Gecersiz => BadRequest("Ret, iptal veya eski kararı değiştirme gerekçesini yazın."),
            DersOnaySonucu.Cakisma => Conflict("Liste değişti; sayfayı yenileyin."), _ => StatusCode(503, "İşlem sonucu doğrulanamadı; otomatik tekrar yapılmadı.") };
    }
}
