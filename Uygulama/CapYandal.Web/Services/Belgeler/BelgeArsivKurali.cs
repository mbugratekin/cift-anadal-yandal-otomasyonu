using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services.Belgeler;

public static class BelgeArsivKurali
{
    public static string TurAdi(KisiselBelgeTuru? tur) => tur switch
    {
        KisiselBelgeTuru.ImzaliForm => "İmzalı form",
        KisiselBelgeTuru.CapYanDalTranskripti => "ÇAP/Yan Dal transkripti",
        KisiselBelgeTuru.AnaDalTranskripti => "Ana dal transkripti",
        KisiselBelgeTuru.YonetimKuruluYazisi => "Yönetim kurulu yazısı",
        KisiselBelgeTuru.Diger => "Diğer belge",
        _ => "Türü belirtilmemiş"
    };
    public static string DurumAdi(BelgeOnayDurumu? durum) => durum switch
    {
        BelgeOnayDurumu.OnayBekliyor => "Onay bekliyor", BelgeOnayDurumu.Onaylandi => "Onaylandı",
        BelgeOnayDurumu.Reddedildi => "Reddedildi", BelgeOnayDurumu.IptalEdildi => "İptal edildi", _ => "İnceleme durumu bilinmiyor"
    };
    public static string IstekDurumuAdi(EkBelgeIstegiDurumu durum) => durum switch
    {
        EkBelgeIstegiDurumu.Bekliyor => "Bekliyor", EkBelgeIstegiDurumu.Yuklendi => "Yüklendi",
        EkBelgeIstegiDurumu.Onaylandi => "Onaylandı", EkBelgeIstegiDurumu.Reddedildi => "Reddedildi",
        EkBelgeIstegiDurumu.IptalEdildi => "İptal edildi", _ => "Bilinmiyor"
    };
}
