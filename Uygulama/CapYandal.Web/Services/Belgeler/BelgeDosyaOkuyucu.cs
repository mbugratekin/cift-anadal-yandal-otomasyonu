using System.Text;
using Microsoft.AspNetCore.Http;

namespace CapYandal.Web.Services.Belgeler;

public sealed record BelgeDosyaSonucu(byte[]? Icerik, string? GuvenliMime, string? Hata)
{
    public bool Basarili => Hata is null && Icerik is not null;
}

// Koordinatör yetkisi/CSRF/controller/DB yerine geçmez. Tek IFormFile iki tipi kabul eder.
public static class BelgeDosyaOkuyucu
{
    public static async Task<BelgeDosyaSonucu> OkuAsync(IFormFile? dosya,
        CancellationToken iptal = default)
    {
        if (dosya is null || !BelgeSurumKurali.DosyaGirdisiGecerli(dosya.FileName, dosya.Length))
            return new(null, null, "PDF veya Word (.docx) seçin; dosya boş olmamalı ve 10 MB’ı aşmamalı.");
        if (dosya.FileName.Length > 250 || dosya.FileName.Any(char.IsControl) ||
            dosya.FileName.IndexOfAny(['/', '\\', ':']) >= 0)
            return new(null, null, "Dosya adı geçersiz.");
        using var hedef = new MemoryStream();
        await using var kaynak = dosya.OpenReadStream();
        byte[] tampon = new byte[8192];
        int adet;
        while ((adet = await kaynak.ReadAsync(tampon.AsMemory(), iptal)) != 0)
        {
            if (hedef.Length + adet > BelgeSurumKurali.DosyaSiniri)
                return new(null, null, "Dosyanın gerçek boyutu 10 MB sınırını aşıyor.");
            await hedef.WriteAsync(tampon.AsMemory(0, adet), iptal);
        }
        if (hedef.Length != dosya.Length)
            return new(null, null, "Dosya boyutu doğrulanamadı.");
        hedef.Position = 0;
        if (Path.GetExtension(dosya.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            if (!DocxPaketKurali.Gecerli(hedef, iptal))
                return new(null, null, "Geçerli, makrosuz bir Word (.docx) belgesi seçin.");
            return new(hedef.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", null);
        }
        var baytlar = hedef.ToArray();
        if (!PdfOnDogrulama(baytlar)) return new(null, null, "PDF biçimi doğrulanamadı.");
        return new(baytlar, "application/pdf", null);
    }

    // İmza/sürüm/son işaret kontrolüdür; tam PDF parserı veya zararsızlık kanıtı değildir.
    private static bool PdfOnDogrulama(byte[] veri)
    {
        var bas = Encoding.ASCII.GetString(veri, 0, Math.Min(veri.Length, 1024));
        int imza = bas.IndexOf("%PDF-", StringComparison.Ordinal);
        if (imza < 0 || imza + 8 > bas.Length) return false;
        var surum = bas.Substring(imza + 5, 3);
        if (surum is not ("1.0" or "1.1" or "1.2" or "1.3" or "1.4" or "1.5" or "1.6" or "1.7" or "2.0"))
            return false;
        if (imza + 8 >= bas.Length || bas[imza + 8] is not ('\r' or '\n')) return false;
        var son = Encoding.ASCII.GetString(veri, Math.Max(0, veri.Length - 1024), Math.Min(veri.Length, 1024));
        return son.TrimEnd(' ', '\t', '\r', '\n').EndsWith("%%EOF", StringComparison.Ordinal);
    }
}

