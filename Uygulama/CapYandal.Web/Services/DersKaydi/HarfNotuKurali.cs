namespace CapYandal.Web.Services.DersKaydi;

// Transkriptteki kod seçilir; sayısal puan başarıya veya harfe otomatik çevrilmez.
public static class HarfNotuKurali
{
    public static readonly IReadOnlyList<string> Guncel = Array.AsReadOnly(new[] { "AA", "BA", "BB", "CB", "CC", "DC", "FF", "F", "G", "K", "M" });
    public static bool Gecerli(string? value) => value is null || value.Length == 0 || Guncel.Contains(value) || value is "DD" or "FD";
}
