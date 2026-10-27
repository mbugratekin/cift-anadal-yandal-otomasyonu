namespace CapYandal.Web.Models;

// Yalnız bilinen sunum üreticisinin öneki gizlenir; kalıcı mesaj ve denetim izi değiştirilmez.
public static class SunumGorunumu
{
    public static string? GerekceMetni(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Equals("Sentetik kullanıcı testi belge incelemesi; gerçek idari onay değildir.", StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var prefix in new[] { "Sentetik test:", "Sentetik kullanıcı testi:", "SENTETİK UÇTAN UCA TEST:", "SENTETİK SUNUM:" })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return text[prefix.Length..].Trim();
        return text;
    }
    public static string BelgeAdi(string name)
    {
        if (name.Equals("CANLI_SENTETIK_DENEME.pdf", StringComparison.OrdinalIgnoreCase))
            return "ogrenci-ek-belgesi.pdf";
        // Yalnız bilinen sunum dosya adı; depo anahtarı, seri ve denetim kaydı değişmez.
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\Akullanici-testi-900000[12][0-9]{2}-.+\.pdf\z")) return name;
        return name["kullanici-testi-".Length..]
            .Replace("tarayici-tam-surum", "belge-v2", StringComparison.Ordinal)
            .Replace("tarayici-yukleme", "belge-v1", StringComparison.Ordinal);
    }
    public static string BelgeSecimiAdi(string label)
    {
        var separator = label.LastIndexOf(" / ", StringComparison.Ordinal);
        return separator < 0 ? BelgeAdi(label) : label[..(separator + 3)] + BelgeAdi(label[(separator + 3)..]);
    }
    public static string SohbetMetni(string text) => GerekceMetni(text
        .Replace("[Sentetik üç yıllık sunum] ", "", StringComparison.Ordinal)
        .Replace("[Sentetik yanıt] ", "", StringComparison.Ordinal)) ?? "";
}
