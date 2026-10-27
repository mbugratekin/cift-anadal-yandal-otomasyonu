using System.Globalization;

namespace CapYandal.Web.Models;

public sealed record GorusmeAraligi(int Gun, string Baslangic, string Bitis)
{
    public string Metin => $"{GunAdi(Gun)} {Baslangic.Replace(':', '.')}–{Bitis.Replace(':', '.')}";
    public static string GunAdi(int gun) => gun switch { 1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba",
        4 => "Perşembe", 5 => "Cuma", 6 => "Cumartesi", 7 => "Pazar", _ => "" };
    public bool Gecerli => Gun is >= 1 and <= 7 && Saat(Baslangic, out var bas) && Saat(Bitis, out var son) && bas < son;
    private static bool Saat(string? value, out TimeOnly time) => TimeOnly.TryParseExact(value, "HH:mm",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
public sealed record GorusmeGirdisi(string? Blok, string? Oda, IReadOnlyList<GorusmeAraligi>? Araliklar, string? Surum)
{
    public string? IletisimEpostasi { get; init; }
}
public sealed record GorusmeBilgisiModel(Guid KullaniciId, string AdSoyad, string Gorev, string Blok, string Oda,
    IReadOnlyList<GorusmeAraligi> Araliklar, string? Surum)
{
    public string? IletisimEpostasi { get; init; }
}
