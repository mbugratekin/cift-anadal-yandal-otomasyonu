namespace CapYandal.Web.Services.Belgeler;

// Genel belgenin kimliği uzantısından ve sürüm dosyasının kimliğinden ayrıdır.
public sealed record GenelBelgeSurumu(Guid BelgeKimligi, int SurumNo,
    bool IcerikMevcut, string DosyaAdi);

public static class BelgeSurumKurali
{
    public const long DosyaSiniri = 10 * 1024 * 1024;

    // Bu kontrol yalnız uzantı/boyut giriş kuralıdır; içerik doğrulaması değildir.
    public static bool DosyaGirdisiGecerli(string? dosyaAdi, long boyut)
    {
        if (string.IsNullOrWhiteSpace(dosyaAdi) || boyut <= 0 || boyut > DosyaSiniri)
            return false;
        var uzanti = Path.GetExtension(dosyaAdi);
        return string.Equals(uzanti, ".pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uzanti, ".docx", StringComparison.OrdinalIgnoreCase);
    }

    public static GenelBelgeSurumu? SonSurum(IEnumerable<GenelBelgeSurumu> surumler,
        Guid belgeKimligi)
    {
        ArgumentNullException.ThrowIfNull(surumler);
        if (belgeKimligi == Guid.Empty) throw new ArgumentException("Belge kimliği zorunlu.");
        var kendi = surumler.Where(s => s.BelgeKimligi == belgeKimligi).ToArray();
        if (kendi.Any(s => s.SurumNo <= 0) ||
            kendi.GroupBy(s => s.SurumNo).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Sürüm geçişi veya tekillik doğrulanmalı.");
        // Son içerik eksik olsa da önceki dosyaya sessizce dönülmez.
        return kendi.MaxBy(s => s.SurumNo);
    }

    public static int SonrakiSurum(IEnumerable<GenelBelgeSurumu> surumler, Guid belgeKimligi)
        => checked((SonSurum(surumler, belgeKimligi)?.SurumNo ?? 0) + 1);
}

