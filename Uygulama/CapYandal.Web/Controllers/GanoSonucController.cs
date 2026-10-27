using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Gano;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class GanoSonucController(GanoSonucArsivServisi archive,
    UserManager<UygulamaKullanicisi> users) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Oku(Guid programId, CancellationToken ct)
    {
        if (!ModelState.IsValid || programId == Guid.Empty) return BadRequest(new { hata = "Geçerli program seçin." });
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Unauthorized();
        var result = await archive.OkuAsync(actor.Id, programId, ct);
        if (result.Durum == GanoArsivDurumu.IzinYok) return Forbid();
        if (result.Durum == GanoArsivDurumu.Bulunamadi) return NotFound();
        if (result.Durum != GanoArsivDurumu.Basarili)
            return Conflict(new { hata = "Değerlendirme geçmişi doğrulanamadı." });
        object Display(GanoArsivSonucu s) => new { s.Id, s.GanoKaydiId, s.DonemId, s.Kapsam,
            s.KosulKumesiId, s.KuralKodu, s.RevizyonNo, s.GanoKopyasi, s.Kod, s.Renk,
            s.GecmisEksikMi, s.NormalEsik, s.AltEsik, s.IzinSayisi, s.DusukDonemSayisi };
        return Ok(new { zincirler = result.Zincirler.Select(z => new {
            guncel = Display(z.Guncel), gecmis = z.Gecmis.Select(Display) }) });
    }
}
