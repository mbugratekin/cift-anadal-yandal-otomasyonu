using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services;

// Ortak onay geçişi — belge ve ders kararlarında aynı durum ve gerekçe denetimi kullanılır.
public static class OnayGecisKurali
{
    public static bool KararGecerli(KayitOnayDurumu mevcut, KayitOnayDurumu hedef, string? gerekce)
    {
        if (!Enum.IsDefined(mevcut) || !Enum.IsDefined(hedef) || mevcut == hedef ||
            hedef == KayitOnayDurumu.OnayBekliyor || gerekce?.Trim().Length > 2000) return false;
        return (mevcut is not (KayitOnayDurumu.Onaylandi or KayitOnayDurumu.IptalEdildi) &&
                hedef is not (KayitOnayDurumu.Reddedildi or KayitOnayDurumu.IptalEdildi)) ||
               !string.IsNullOrWhiteSpace(gerekce);
    }

    public static bool OgrenciGonderebilir(KayitOnayDurumu mevcut, bool dahaOnceKilitlendi) =>
        !dahaOnceKilitlendi && mevcut is KayitOnayDurumu.OnayBekliyor or KayitOnayDurumu.Reddedildi;
}
