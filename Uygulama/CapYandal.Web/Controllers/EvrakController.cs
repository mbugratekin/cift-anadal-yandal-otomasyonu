using System.Security.Claims;
using CapYandal.Web.Security;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapYandal.Web.Controllers;
[Authorize,AutoValidateAntiforgeryToken,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class EvrakController(EvrakKayitServisi uploads) : Controller
{
    [HttpPost,Authorize(Roles=UygulamaRolleri.Ogrenci+","+UygulamaRolleri.Koordinator),RequestSizeLimit(11534336),RequestFormLimits(MultipartBodyLengthLimit=11534336)]
    public async Task<IActionResult> Yukle(Guid programId,Guid donemId,Guid? listeId,IFormFile? file,KisiselBelgeTuru? tur,Guid? seriId,Guid? istekId,string? gerekce,CancellationToken ct) {
        if(!ModelState.IsValid || Request.Form.Files.Count!=1 || tur is null || !Enum.IsDefined(tur.Value)) return BadRequest(new {hata="Belge türü ve tek PDF veya Word (.docx) belgesi seçin."});
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)) return Challenge();
        var result=await uploads.GonderAsync(actor,programId,donemId,listeId,file,tur.Value,seriId,istekId,ct,gerekce);
        return Cevap(result.Kod,null);
    }
    private IActionResult Cevap(EvrakKayitKodu result,string? surum)=>result switch {
        EvrakKayitKodu.Basarili=>Ok(new {kaydedildi=true,kaldirildi=false,surum}),
        EvrakKayitKodu.IzinYok=>Forbid(),EvrakKayitKodu.Bulunamadi=>NotFound(new {hata="Dosya veya program bulunamadı."}),
        EvrakKayitKodu.Gecersiz=>BadRequest(new {hata="Dosya, dönem ve program bilgilerini kontrol edin. Tek PDF/DOCX, en fazla 10 MB.",surum}),
        EvrakKayitKodu.Cakisma=>Conflict(new {hata="Dosya değişti. Sayfayı yenileyip güncel durumu kontrol edin."}),
        _=>StatusCode(503,new {hata="İşlem tamamlanamadı. Dosya durumu korunuyor; otomatik tekrar yapılmadı.",surum})
    };
}
