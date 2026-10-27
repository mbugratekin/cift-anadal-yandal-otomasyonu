namespace CapYandal.Web.Services.DersKaydi;

public static class KayitSurumKurali
{
    // SQL rowversion yalnız eşitlik/concurrency içindir; tarih veya sıralama sayılmaz.
    public static bool KomutGuncel(Guid kayitDonemi, Guid istenenDonem,
        ReadOnlySpan<byte> mevcutSurum, string? beklenenSurum)
    {
        if (kayitDonemi == Guid.Empty || kayitDonemi != istenenDonem ||
            mevcutSurum.Length != 8 || string.IsNullOrEmpty(beklenenSurum) || beklenenSurum.Length > 16)
            return false;
        Span<byte> decoded = stackalloc byte[8];
        return Convert.TryFromBase64String(beklenenSurum, decoded, out var count) &&
            count == 8 && mevcutSurum.SequenceEqual(decoded);
    }
}
