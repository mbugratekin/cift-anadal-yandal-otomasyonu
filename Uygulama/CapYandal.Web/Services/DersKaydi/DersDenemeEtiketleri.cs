using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services.DersKaydi;

// Durum etiketleri — saklanan kodlar değişmeden anlaşılır Türkçe metne çevrilir.
public static class DersDenemeEtiketleri
{
    public static string Durum(DersDenemeDurumu value) => value switch
    {
        DersDenemeDurumu.Planlandi => "Planlandı", DersDenemeDurumu.Alindi => "Alındı",
        DersDenemeDurumu.Gecti => "Geçti", DersDenemeDurumu.Kaldi => "Kaldı",
        DersDenemeDurumu.Muaf => "Muaf", DersDenemeDurumu.Cikarildi => "Çıkarıldı", _ => "Bilinmiyor"
    };
    public static string Onay(KayitOnayDurumu value) => value switch
    {
        KayitOnayDurumu.OnayBekliyor => "Onay bekliyor", KayitOnayDurumu.Onaylandi => "Onaylandı",
        KayitOnayDurumu.Reddedildi => "Reddedildi", KayitOnayDurumu.IptalEdildi => "İptal", _ => "Bilinmiyor"
    };
}
