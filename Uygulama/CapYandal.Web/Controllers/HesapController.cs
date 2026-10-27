using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Hesap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Controllers;

[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class HesapController(UserManager<UygulamaKullanicisi> users,
    SignInManager<UygulamaKullanicisi> signIn, HesapErisimi access, AppDbContext database,
    ISifreSifirlamaServisi sifreSifirlama, IKisiselEpostaKurulumServisi kisiselEpostaKurulumu, IlkDavetServisi ilkDavet,
    PersonelErisimServisi personelErisimi, ILogger<HesapController> logger) : Controller
{
    private const string GirisHatasi = "Bilgilerinizi kontrol edip tekrar deneyin.";

    // Süresi dolmuş POST işlemi giriş sonrası GET olarak yeniden açılmaz bilinen okuma ekranlarına dönülür.
    private bool GirisDonusuGuvenli(string? address)
    {
        if (!Url.IsLocalUrl(address)) return false;
        var path = address!.Split('?', '#')[0];
        if (path.StartsWith("~/", StringComparison.Ordinal)) path = path[1..];
        return path.StartsWith("/Panel/", StringComparison.OrdinalIgnoreCase) ||
            new[] { "/PersonelHesap", "/PersonelHesap/Index", "/IslemGecmisi", "/IslemGecmisi/Index", "/Hesap/ParolaDegistir" }
                .Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> ParolaOlustur(Guid userId, string? davetId, string? token, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !await ilkDavet.GecerliMiAsync(userId, davetId, token, cancellationToken))
            return View("IlkDavetGecersiz");
        return View(new IlkParolaModel { UserId = userId, DavetId = davetId!, Token = token! });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> ParolaOlustur(IlkParolaModel model, CancellationToken cancellationToken)
    {
        if (!await ilkDavet.GecerliMiAsync(model.UserId, model.DavetId, model.Token, cancellationToken))
            return View("IlkDavetGecersiz");
        if (ModelState.IsValid)
        {
            var result = await ilkDavet.OlusturAsync(model, cancellationToken);
            if (result == SifreSifirlamaSonucu.Basarili)
            {
                return RedirectToAction(nameof(Giris), new { yeniParola = "true" });
            }
            if (result == SifreSifirlamaSonucu.BaglantiGecersiz) return View("IlkDavetGecersiz");
            ModelState.AddModelError(string.Empty, "Parola koşulları sağlanmadı. Tekrar deneyin.");
        }
        model.YeniParola = model.YeniParolaTekrar = string.Empty;
        ModelState.Remove(nameof(model.YeniParola));
        ModelState.Remove(nameof(model.YeniParolaTekrar));
        return View(model);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Giris(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(Index));
        return View(new GirisModel { DonusAdresi = GirisDonusuGuvenli(returnUrl) ? returnUrl : null });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> Giris(GirisModel model)
    {
        if (!await GuvenlikTalebiKaydiAsync("GirisTalebi"))
        {
            model.Parola = string.Empty; ModelState.SetModelValue(nameof(GirisModel.Parola), null, null);
            ModelState.AddModelError(string.Empty, "Giriş işlemi şu anda tamamlanamadı.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return View(model);
        }
        if (ModelState.IsValid)
        {
            var user = await users.FindByNameAsync(model.KullaniciAdi);
            if (user is not null && (await access.OkuAsync(user)).Izinli)
            {
                var oncekiOturumAcik = User.Identity?.IsAuthenticated == true;
                var result = await signIn.PasswordSignInAsync(user, model.Parola, isPersistent: false, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    if (await OturumKaydiAsync(user.Id, "OturumGiris", oncekiOturumAcik, true))
                        return GirisDonusuGuvenli(model.DonusAdresi)
                            ? LocalRedirect(model.DonusAdresi!) : RedirectToAction(nameof(Index));
                    await signIn.SignOutAsync();
                    Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }
            }
            ModelState.AddModelError(string.Empty, GirisHatasi);
        }
        model.Parola = string.Empty;
        ModelState.SetModelValue(nameof(GirisModel.Parola), null, null);
        return View(model);
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole(UygulamaRolleri.Koordinator) || User.IsInRole(UygulamaRolleri.BolumBaskani))
            return RedirectToAction("Ogrenciler", "Panel");
        if (User.IsInRole(UygulamaRolleri.Ogrenci)) return RedirectToAction("Ogrenci", "Panel");
        return Forbid();
    }

    [HttpPost, HesapKurulumunaIzin]
    public async Task<IActionResult> Cikis()
    {
        var user = await users.GetUserAsync(User);
        var recorded = user is not null && await OturumKaydiAsync(user.Id, "OturumCikis", true, false);
        // Denetim erişilemiyorsa da çıkış engellenmez; hata başarı gibi gösterilmez.
        await signIn.SignOutAsync();
        if (!recorded) return StatusCode(503, "Oturum kapatıldı, ancak işlem kaydı tamamlanamadı.");
        return RedirectToAction(nameof(Giris));
    }

    
    private Task<bool> OturumKaydiAsync(Guid userId, string operation, bool onceki, bool yeni)
    {
        var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = "Oturum",
            VarlikAnahtari = userId.ToString(), YapanKullaniciId = userId, IslemAni = DateTimeOffset.UtcNow, Islem = operation,
            OncekiDeger = onceki ? "{\"OturumAcik\":true}" : "{\"OturumAcik\":false}",
            YeniDeger = yeni ? "{\"OturumAcik\":true}" : "{\"OturumAcik\":false}" };
        return DenetimYazAsync(audit);
    }

    // Formdaki hesap adı alıcı veya aktör sayılmaz; yalnız talebin alındığı kaydedilir.
    private Task<bool> GuvenlikTalebiKaydiAsync(string operation)
    {
        var id = Guid.NewGuid();
        Guid? actor = User.Identity?.IsAuthenticated == true && Guid.TryParse(users.GetUserId(User), out var known) ? known : null;
        return DenetimYazAsync(new() { Id = id, VarlikTuru = "GuvenlikTalebi", VarlikAnahtari = id.ToString(),
            Islem = operation, YapanKullaniciId = actor, IslemAni = DateTimeOffset.UtcNow, OncekiDeger = "null",
            YeniDeger = "{\"TalepAlindi\":true}" });
    }

    private async Task<bool> DenetimYazAsync(DegisiklikGecmisi audit)
    {
        if (database.ChangeTracker.HasChanges()) return false;
        try
        {
            database.DegisiklikGecmisi.Add(audit);
            await database.SaveChangesAsync(CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            database.Entry(audit).State = EntityState.Detached;
            logger.LogError("Hesap denetim kaydı tamamlanamadı.");
            return false;
        }
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci), HesapKurulumunaIzin]
    public async Task<IActionResult> Kurulum()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return RedirectToAction("Ogrenci", "Panel");
    }

    [HttpPost, Authorize(Roles = UygulamaRolleri.Ogrenci), EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> KisiselEpostaKaydet(HesapKurulumModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { hata = "Geçerli bir e-posta adresi yazın." });
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var adres = model.KisiselEposta.Trim();
        if (adres == (user.BekleyenKisiselEposta ?? user.KisiselEposta))
            return Ok(new { degisti = false });
        var sonuc = await kisiselEpostaKurulumu.TalepEtAsync(user.Id, adres, cancellationToken);
        if (sonuc == KisiselEpostaTalepSonucu.IzinYok) return Forbid();
        if (sonuc == KisiselEpostaTalepSonucu.Basarisiz)
            return StatusCode(503, new { hata = "İşlem tamamlanamadı. Tekrar deneyin." });
        return Ok(new { degisti = true, dogrulamaGonderildi = sonuc == KisiselEpostaTalepSonucu.Gonderildi });
    }

    [HttpPost, Authorize(Roles = UygulamaRolleri.Ogrenci), HesapKurulumunaIzin, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> Kurulum(HesapKurulumModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await kisiselEpostaKurulumu.TalepEtAsync(user.Id, model.KisiselEposta, cancellationToken);
        if (result == KisiselEpostaTalepSonucu.IzinYok) return Forbid();
        if (result is KisiselEpostaTalepSonucu.Gonderildi or KisiselEpostaTalepSonucu.DevreDisi)
        {
            TempData["Basari"] = result == KisiselEpostaTalepSonucu.Gonderildi
                ? "Kişisel adresiniz kaydedildi. Doğrulama bağlantısı e-posta sunucusuna iletildi; doğrulamayı beklemeden uygulamayı kullanabilirsiniz."
                : "Kişisel adresiniz kaydedildi, ancak doğrulama e-postası gönderilmedi. Adresiniz doğrulanmamış olarak kalır; uygulamayı kullanabilirsiniz.";
            return RedirectToAction(nameof(Index));
        }
        ModelState.AddModelError(string.Empty, "İşlem tamamlanamadı. Adresinizi kontrol edip tekrar deneyin.");
        return View(model);
    }

    [AllowAnonymous, HttpGet, HesapKurulumunaIzin]
    public async Task<IActionResult> KisiselEpostaDogrula(Guid userId, string? requestId, string? token, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("KisiselEpostaBaglantisiGecersiz");
        var model = new KisiselEpostaDogrulamaModel { UserId = userId, RequestId = requestId ?? string.Empty, Token = token ?? string.Empty };
        model.Adres = await kisiselEpostaKurulumu.BekleyenAdresiOkuAsync(model.UserId, model.RequestId, model.Token, cancellationToken);
        return model.Adres is null ? View("KisiselEpostaBaglantisiGecersiz") : View(model);
    }

    [AllowAnonymous, HttpPost, HesapKurulumunaIzin, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> KisiselEpostaDogrula(KisiselEpostaDogrulamaModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            !await kisiselEpostaKurulumu.DogrulaAsync(model.UserId, model.RequestId, model.Token, cancellationToken))
            return View("KisiselEpostaBaglantisiGecersiz");

        TempData["Basari"] = "Kişisel e-posta adresiniz doğrulandı.";
        var currentUser = await users.GetUserAsync(User);
        if (currentUser is not null && currentUser.Id == model.UserId)
        {
            await signIn.RefreshSignInAsync(currentUser);
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Giris));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult SifremiUnuttum() => View(new SifreSifirlamaTalebiModel());

    [AllowAnonymous, HttpPost, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> SifremiUnuttum(
        SifreSifirlamaTalebiModel model,
        CancellationToken cancellationToken)
    {
        if (!await GuvenlikTalebiKaydiAsync("ParolaSifirlamaTalebi"))
            return StatusCode(503, "İşlem şu anda tamamlanamadı. Daha sonra tekrar deneyin.");
        if (!ModelState.IsValid)
            return View(model);

        await sifreSifirlama.TalepEtAsync(model.KullaniciAdi, cancellationToken);
        await personelErisimi.TalepEtAsync(model.KullaniciAdi, cancellationToken);
        return RedirectToAction(nameof(SifremiUnuttumSonuc));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult SifremiUnuttumSonuc() => View();

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> SifreSifirla(
        Guid userId,
        string? resetId,
        string? token,
        CancellationToken cancellationToken)
    {
        if (!await sifreSifirlama.BaglantiGecerliMiAsync(userId, resetId, token, cancellationToken))
            return View("SifreSifirlamaGecersiz");

        return View(new SifreSifirlamaModel { UserId = userId, ResetId = resetId!, Token = token! });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> SifreSifirla(
        SifreSifirlamaModel model,
        CancellationToken cancellationToken)
    {
        var linkValid = await sifreSifirlama.BaglantiGecerliMiAsync(
            model.UserId, model.ResetId, model.Token, cancellationToken);
        if (!linkValid)
            return View("SifreSifirlamaGecersiz");

        if (!ModelState.IsValid)
            return View(model);

        var result = await sifreSifirlama.SifirlaAsync(model, cancellationToken);
        if (result == SifreSifirlamaSonucu.Basarili)
        {
            TempData.Remove("Basari");
            return RedirectToAction(nameof(Giris));
        }

        if (result == SifreSifirlamaSonucu.ParolaKosullariUygunDegil)
        {
            ModelState.AddModelError(string.Empty, "Yeni parola parola koşullarına uygun değil.");
            model.YeniParola = string.Empty;
            model.YeniParolaTekrar = string.Empty;
            ModelState.Remove(nameof(model.YeniParola));
            ModelState.Remove(nameof(model.YeniParolaTekrar));
            return View(model);
        }

        return View("SifreSifirlamaGecersiz");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Yetkisiz()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet, HesapKurulumunaIzin]
    public IActionResult ParolaDegistir()
    {
        if (HttpContext.Items[HesapOturumu.ErisimAnahtari] is HesapErisimDurumu { OgrenciKurulumuGerekli: true })
            return RedirectToAction(nameof(Kurulum));
        return View(new ParolaDegistirModel());
    }

    [HttpPost, HesapKurulumunaIzin, EnableRateLimiting(HesapGuvenligi.GirisSiniri)]
    public async Task<IActionResult> ParolaDegistir(ParolaDegistirModel model)
    {
        if (HttpContext.Items[HesapOturumu.ErisimAnahtari] is HesapErisimDurumu { OgrenciKurulumuGerekli: true })
            return RedirectToAction(nameof(Kurulum));
        if (ModelState.IsValid)
        {
            var user = await users.GetUserAsync(User);
            if (user is null) return Challenge();
            await using var transaction = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync() : null;
            var tx = transaction ?? database.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (transaction is null) await tx.CreateSavepointAsync(point);
            var completed = false; DegisiklikGecmisi? audit = null;
            try
            {
                var oncekiDurum = System.Text.Json.JsonSerializer.Serialize(new { user.ParolaDegisikligiZorunlu, GeciciParolaVarMi = user.GeciciParolaBitisAni is not null });
                var result = await users.ChangePasswordAsync(user, model.MevcutParola, model.YeniParola);
                if (result.Succeeded)
                {
                    user.ParolaDegisikligiZorunlu = false;
                    user.GeciciParolaBitisAni = null;
                    result = await users.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        audit = new()
                        {
                            Id = Guid.NewGuid(),
                            VarlikTuru = nameof(UygulamaKullanicisi),
                            VarlikAnahtari = user.Id.ToString(),
                            Islem = "ParolaDegistir",
                            YapanKullaniciId = user.Id,
                            IslemAni = DateTimeOffset.UtcNow,
                            OncekiDeger = oncekiDurum,
                            YeniDeger = System.Text.Json.JsonSerializer.Serialize(new { user.ParolaDegisikligiZorunlu, GeciciParolaVarMi = user.GeciciParolaBitisAni is not null, ParolaYenilendiMi = true })
                        };
                        database.DegisiklikGecmisi.Add(audit);
                        await database.SaveChangesAsync();
                        if (transaction is not null) await transaction.CommitAsync();
                        completed = true;
                        if ((await users.GetRolesAsync(user)).Count == 0)
                        {
                            await signIn.SignOutAsync();
                            TempData["Basari"] = "Parolanız belirlendi. Görev devri tamamlandıktan sonra giriş yapabilirsiniz.";
                            return RedirectToAction(nameof(Giris));
                        }
                        await signIn.RefreshSignInAsync(user);
                        TempData["Basari"] = "Parolanız değiştirildi.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                ModelState.AddModelError(string.Empty,
                    "Parola değiştirilemedi. Mevcut parolanızı ve yeni parolanın koşullarını kontrol edin.");
            }
            finally
            {
                if (!completed)
                {
                    if (transaction is not null) await tx.RollbackAsync(); else await tx.RollbackToSavepointAsync(point);
                    if (audit is not null) database.Entry(audit).State = EntityState.Detached;
                    await database.Entry(user).ReloadAsync();
                }
            }
        }
        ModelState.SetModelValue(nameof(model.MevcutParola), null, null);
        ModelState.SetModelValue(nameof(model.YeniParola), null, null);
        ModelState.SetModelValue(nameof(model.YeniParolaTekrar), null, null);
        return View(new ParolaDegistirModel());
    }
}
