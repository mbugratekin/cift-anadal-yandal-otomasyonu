using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services;

public sealed record GanoEsikSonucu(GanoSonucKodu Kod, GanoSonucRengi? Renk, bool GecmisEksikMi,
    int? BilinenDusukDonemSayisi);

// GANO değerini karşılaştırır; kayıt veya akademik karar oluşturmaz.
public static class GanoEsikKontrolu
{
    public static GanoEsikSonucu? CapDevam(decimal? gano, decimal normalEsik, decimal? altEsik,
        int izinSayisi, int oncekiBilinenDusukDonemSayisi, bool gecmisEksikMi, bool etkinMi = true)
    {
        GecerliDeger(normalEsik, nameof(normalEsik));
        if (altEsik is decimal minimum)
        {
            GecerliDeger(minimum, nameof(altEsik));
            if (minimum > normalEsik) throw new ArgumentException("Alt eşik normal eşikten büyük olamaz.", nameof(altEsik));
        }
        if (izinSayisi < 0) throw new ArgumentOutOfRangeException(nameof(izinSayisi));
        if (oncekiBilinenDusukDonemSayisi < 0) throw new ArgumentOutOfRangeException(nameof(oncekiBilinenDusukDonemSayisi));
        if (gano is null) return null; // An absent entry is not a result or a green period.
        GecerliDeger(gano.Value, nameof(gano));
        if (!etkinMi) return new(GanoSonucKodu.DevreDisi, null, gecmisEksikMi, null);
        // DÜŞME HAKKI: Önce kesin ihlal, sonra eksik geçmiş, en son kullanılabilir istisna değerlendirilir.
        // Eşikler parametredir; bu metotta 2,72 / 2,50 gibi kuruma ait sayılar sabitlenmez.
        var low = gano.Value < normalEsik;
        var count = checked(oncekiBilinenDusukDonemSayisi + (low ? 1 : 0));
        if (!low) return new(GanoSonucKodu.Normal, GanoSonucRengi.Yesil, gecmisEksikMi, count);
        if ((altEsik is decimal floor && gano.Value < floor) || count > izinSayisi)
            return new(GanoSonucKodu.Ihlal, GanoSonucRengi.Kirmizi, gecmisEksikMi, count);
        if (gecmisEksikMi) return new(GanoSonucKodu.BelirsizGecmisEksik, null, true, count);
        return new(GanoSonucKodu.Istisna, GanoSonucRengi.Sari, false, count);
    }

    public static GanoEsikSonucu? TekEsik(decimal? gano, decimal esik, bool gecmisEksikMi = false, bool etkinMi = true)
    {
        GecerliDeger(esik, nameof(esik));
        if (gano is null) return null;
        GecerliDeger(gano.Value, nameof(gano));
        if (!etkinMi) return new(GanoSonucKodu.DevreDisi, null, gecmisEksikMi, null);
        return gano.Value >= esik
            ? new(GanoSonucKodu.Normal, GanoSonucRengi.Yesil, gecmisEksikMi, null)
            : new(GanoSonucKodu.Ihlal, GanoSonucRengi.Kirmizi, gecmisEksikMi, null);
    }

    private static void GecerliDeger(decimal value, string parameter)
    {
        if (value is < 0m or > 4m)
            throw new ArgumentOutOfRangeException(parameter, "4'lük sistemde 0–4 aralığında değer gerekir.");
    }
}
