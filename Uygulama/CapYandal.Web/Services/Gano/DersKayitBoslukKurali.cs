namespace CapYandal.Web.Services.Gano;

// Bu sayı kayıt eksikliği uyarısıdır, kesin ilişik kesme kanıtı değildir.
public static class DersKayitBoslukKurali
{
    public static int? Say(int? kabul, int islemDonemi, IReadOnlySet<int> onayliDersDonemleri,
        IReadOnlySet<int> bekleyenDonemler, IReadOnlyList<(int Baslangic, int Bitis)> izinler)
    {
        if (kabul is null || kabul > islemDonemi) return null;
        var count = 0;
        for (var term = islemDonemi - 1; term >= kabul; term--)
        {
            if (izinler.Any(x => x.Baslangic <= term && term <= x.Bitis) || onayliDersDonemleri.Contains(term)) break;
            if (bekleyenDonemler.Contains(term)) return null;
            count++;
        }
        return count;
    }
}
