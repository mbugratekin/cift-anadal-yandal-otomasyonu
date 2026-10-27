namespace CapYandal.Web.Security;

public static class UygulamaRolleri
{
    public const string Ogrenci = "Ogrenci";
    public const string Koordinator = "Koordinator";
    public const string BolumBaskani = "BolumBaskani";

    // Her hesap yalnız bir rolle çalışır.
    public static bool GecerliTekRol(IReadOnlyCollection<string> roller) =>
        roller.Count == 1 && roller.Single() is Ogrenci or Koordinator or BolumBaskani;
}
