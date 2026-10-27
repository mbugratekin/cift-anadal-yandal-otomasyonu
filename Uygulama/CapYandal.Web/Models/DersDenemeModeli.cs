using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

// Formdan kayıt kimliği ve sürümü alınır; kullanıcı ve zaman sunucuda belirlenir.
public sealed class DersDenemeModeli : IValidatableObject
{
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid DersSecimKalemiId { get; set; }
    public Guid? DenemeId { get; set; }
    public int BeklenenSurum { get; set; }
    public DersDenemeDurumu Durum { get; set; }
    public KayitOnayDurumu OnayDurumu { get; set; } = KayitOnayDurumu.OnayBekliyor;
    public bool GecmisAktarim { get; set; }
    [StringLength(32)] public string? Not { get; set; }
    [Range(0,100), Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(SayisalNotModelBinder))] public decimal? SayisalNot { get; set; }
    public Guid? GecilenDonemId { get; set; }
    [StringLength(2000)] public string? Gerekce { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProgramId == Guid.Empty || DonemId == Guid.Empty || DersSecimKalemiId == Guid.Empty || DenemeId == Guid.Empty)
            yield return new("Ders, program ve dönem gerekli.");
        if (!Enum.IsDefined(Durum) || !Enum.IsDefined(OnayDurumu)) yield return new("Geçerli kayıt durumu seçin.");
        if (!CapYandal.Web.Services.DersKaydi.HarfNotuKurali.Gecerli(Not)) yield return new("Transkriptteki harf notunu seçin.");
        if (SayisalNot is { } score && decimal.Round(score,2) != score) yield return new("Sayısal not en fazla iki ondalık olmalı.");
        if (OnayDurumu == KayitOnayDurumu.Onaylandi && Not == "M" && Durum != DersDenemeDurumu.Muaf)
            yield return new("M notu için Muaf durumunu seçin.");
        if (OnayDurumu == KayitOnayDurumu.Onaylandi && !string.IsNullOrEmpty(Not) && Durum is DersDenemeDurumu.Alindi or DersDenemeDurumu.Planlandi)
            yield return new("Notu onaylamadan önce Geçti, Kaldı veya Muaf sonucunu belirleyin.");
        if (OnayDurumu == KayitOnayDurumu.Onaylandi && Durum is DersDenemeDurumu.Gecti or DersDenemeDurumu.Kaldi &&
            (Not is "FF" or "F" or "K" or "FD" && Durum != DersDenemeDurumu.Kaldi || Not is "AA" or "BA" or "BB" or "CB" or "CC" or "G" && Durum != DersDenemeDurumu.Gecti))
            yield return new("Harf notu ile ders sonucu uyuşmuyor. Transkripti kontrol edin.");
        if ((DenemeId is null && BeklenenSurum != 0) || (DenemeId is not null && BeklenenSurum < 1))
            yield return new("Güncel deneme sürümü gerekli.");
        if ((Durum == DersDenemeDurumu.Gecti) != (GecilenDonemId is not null && GecilenDonemId != Guid.Empty))
            yield return new("Geçilen dönem yalnız geçti durumunda gerekli.");
        if (Durum != DersDenemeDurumu.Gecti && GecilenDonemId is not null) yield return new("Geçilen dönem bu durumda kullanılamaz.");
        if (OnayDurumu is KayitOnayDurumu.Reddedildi or KayitOnayDurumu.IptalEdildi || GecmisAktarim)
            if (string.IsNullOrWhiteSpace(Gerekce)) yield return new("Ret, iptal veya geçmiş aktarım gerekçesini yazın.");
        if (GecmisAktarim && (DenemeId is not null || OnayDurumu != KayitOnayDurumu.Onaylandi))
            yield return new("Geçmiş aktarımı yalnız ilk ve onaylı kayıtta kullanılabilir.");
    }
}
