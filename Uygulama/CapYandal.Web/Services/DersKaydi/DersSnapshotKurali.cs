using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services.DersKaydi;

public sealed record DersSnapshot(Guid DersId, string Kod, string Ad, decimal Akts, decimal Kredi, DersTuru Tur, int MufredatYariyili);

public static class DersSnapshotKurali
{
    // Ders bilgileri formdan değil, kayıtlı müfredattan alınır.
    // Eski liste kalemlerini bugünkü şablondan doldurmak için kullanılmaz.
    public static bool Olustur(SablonDersi source, Guid templateId, out DersSnapshot? snapshot)
    {
        snapshot = null;
        if (templateId == Guid.Empty || source.SablonId != templateId || source.DersId == Guid.Empty || !source.EtkinMi ||
            source.Kod is not { Length: 7 } code || code.Any(c => c is < '0' or > '9') ||
            string.IsNullOrWhiteSpace(source.Ad) || source.Ad.Length > 250 || source.Ad != source.Ad.Trim() ||
            source.MufredatYariyili is < 1 or > 8 || !Enum.IsDefined(source.Tur) ||
            source.Akts is not { } akts || !Sayi(akts) || source.Kredi is not { } kredi || !Sayi(kredi)) return false;
        snapshot = new(source.DersId, code, source.Ad, akts, kredi, source.Tur, source.MufredatYariyili);
        return true;
    }

    private static bool Sayi(decimal value) => value is >= 0 and <= 9999.99m && decimal.Round(value, 2) == value;
}
