using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Hesap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;
[Authorize(Roles = UygulamaRolleri.Koordinator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class GorusmeController(GorusmeBilgisiServisi service, UserManager<UygulamaKullanicisi> users) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(8192)]
    public async Task<IActionResult> Kaydet([FromBody] GorusmeGirdisi? input, CancellationToken ct)
    {
        if (input is null || !ModelState.IsValid) return BadRequest(new { hata = "Blok, oda ve gün–saat aralıklarını kontrol edin." });
        var user = await users.GetUserAsync(User); if (user is null) return Challenge();
        var answer = await service.KaydetAsync(user.Id, input, ct);
        return answer.Durum == 200 ? Ok(new { kaydedildi = true, surum = answer.Surum }) : StatusCode(answer.Durum,
            new { hata = answer.Durum switch { 409 => "Bilgi başka bir işlemde değişti. Sayfayı yenileyin.", 403 => "Yalnız kendi görevli bilginizi düzenleyebilirsiniz.",
                400 => "Her gün için bitiş başlangıçtan sonra olmalıdır. Alanları kontrol edin.", _ => "İşlem doğrulanamadı. Tekrar göndermeden sayfayı yenileyin." } });
    }
}
