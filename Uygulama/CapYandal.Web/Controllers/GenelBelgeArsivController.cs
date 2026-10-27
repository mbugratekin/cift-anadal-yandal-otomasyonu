using System.Security.Claims;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;

[Authorize(Roles=UygulamaRolleri.Koordinator)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class GenelBelgeArsivController(GenelBelgeArsivServisi service):Controller
{
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Kaydet(Guid belgeId,string? surum,bool? arsivle,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var actor))return Challenge();
        if(!ModelState.IsValid || arsivle is null)return BadRequest();
        byte[]? token;try{token=surum is null?null:Convert.FromBase64String(surum);}catch(FormatException){return BadRequest();}
        var result=await service.KaydetAsync(actor,belgeId,token,arsivle.Value,ct);
        if(result==GenelBelgeKayitKodu.IzinYok)return Forbid();
        if(Request.Headers.Accept.Any(x=>x?.Contains("application/json",StringComparison.OrdinalIgnoreCase)==true))
            return result==GenelBelgeKayitKodu.Basarili ? Ok(new {kaydedildi=true}) :
                StatusCode(result==GenelBelgeKayitKodu.Cakisma?409:400,new {hata="Belge değişmiş veya işlem tamamlanamamış. Güncel durumu kontrol edin; otomatik tekrar yapılmadı."});
        TempData["GenelBelgeSonucu"]=result switch { GenelBelgeKayitKodu.Basarili=>arsivle.Value?"Belge arşive kaldırıldı. İçeriği ve geçmişi korunuyor.":"Belge arşivden geri alındı.",GenelBelgeKayitKodu.Cakisma=>"Belge değişti; güncel sayfadan tekrar kontrol edin.",_=>"İşlem doğrulanamadı. Tekrar göndermeden güncel durumu kontrol edin."};
        return RedirectToAction("Dokumanlar","Panel",new {yeni=true});
    }
}
