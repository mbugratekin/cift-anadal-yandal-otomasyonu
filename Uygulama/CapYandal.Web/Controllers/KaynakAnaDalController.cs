using System.Security.Claims;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Dersler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator)]
public sealed class KaynakAnaDalController(KaynakAnaDalServisi service) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(Guid? id, OgrenciProgramTuru tur, string? ad, bool etkinMi, string? surum, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return BadRequest();
        var status = await service.KaydetAsync(actor, id, tur, ad, etkinMi, surum, ct);
        if (status != 200) return StatusCode(status, "Ana dal kaydedilemedi. Alanları, yetkiyi ve güncel sürümü kontrol edin.");
        return RedirectToAction("AkademikKosullar", "Panel");
    }
}
