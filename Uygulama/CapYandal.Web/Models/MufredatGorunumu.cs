using System.Text.RegularExpressions;

namespace CapYandal.Web.Models;

// Yıl aralığı görünüm metninden ayrılır kimlik atama ve kayıtlı ad değiştirilmez.
public static class MufredatGorunumu
{
    public static string Ad(string? ad) => string.IsNullOrWhiteSpace(ad) ? "Adlandırılmamış müfredat"
        : Regex.Replace(ad.Trim(), @"\s+\([0-9]{4} (?:öncesi|ve sonrası)\)$", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Şart kapsamı: program/bölüm bilgisini müfredat adından tekrar etmeden tek satırda gösterir.
    public static string Kapsam(string program, string? anaDal, string? mufredatAdi)
    {
        var ad = Ad(mufredatAdi);
        var yil = Regex.Match(ad, @"\b[0-9]{4}[–-][0-9]{4}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Value;
        var parts = new[] { program, anaDal, string.IsNullOrWhiteSpace(yil) ? ad : yil }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" / ", parts);
    }
}
