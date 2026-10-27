using System.Security.Claims;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Gano;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SayisalKosulController(SayisalKosulServisi servis) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Oku(Guid programId, Guid donemId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await servis.OkuAsync(actor, programId, donemId, ct);
        return result.Durum == 403 ? Forbid() : Json(result);
    }
    [HttpPost, Authorize(Roles = UygulamaRolleri.Koordinator), ValidateAntiForgeryToken]
    public async Task<IActionResult> Kontrol(Guid programId, Guid donemId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Challenge();
        var result = await servis.KontrolAsync(actor, programId, donemId, ct);
        return result.Durum == 403 ? Forbid() : StatusCode(result.Durum, result);
    }
}
