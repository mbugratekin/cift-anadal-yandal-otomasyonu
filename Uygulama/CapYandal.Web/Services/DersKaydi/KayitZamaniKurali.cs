using System.Globalization;

namespace CapYandal.Web.Services.DersKaydi;

public static class KayitZamaniKurali
{
    private static readonly TimeZoneInfo Turkiye = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // null: alan eksik, işlem reddedilir. "": bilinçli boş tarih, hemen işlem.
    public static bool Oku(string? text, out DateTimeOffset? utc)
    {
        utc = null;
        if (text is null) return false;
        if (text.Length == 0) return true;
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)) return false;
        var local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        if (Turkiye.IsInvalidTime(local) || Turkiye.IsAmbiguousTime(local)) return false;
        try { utc = new DateTimeOffset(local, Turkiye.GetUtcOffset(local)).ToUniversalTime(); }
        catch (ArgumentException) { return false; }
        return true;
    }

    public static string Goster(DateTimeOffset? utc) => utc is { } value
        ? TimeZoneInfo.ConvertTime(value, Turkiye).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
        : "";

    // Yetki ve expected RowVersion doğrulaması gerçek serviste ayrıca zorunludur.
    public static bool Baslat(KapiDurumu durum, Guid donemId, DateTimeOffset sunucuAni,
        string? baslangicAlani, string? bitisAlani, out KapiDurumu next)
    {
        next = durum;
        if (!Oku(baslangicAlani, out var start) || !Oku(bitisAlani, out var end)) return false;
        return KayitKapisiKurali.Baslat(durum, donemId, sunucuAni, start, end, out next);
    }

    public static bool Kapat(KapiDurumu durum, Guid donemId, DateTimeOffset sunucuAni,
        string? bitisAlani, out KapiDurumu next)
    {
        next = durum;
        if (!Oku(bitisAlani, out var end)) return false;
        return KayitKapisiKurali.Kapat(durum, donemId, sunucuAni, end, out next);
    }
}
