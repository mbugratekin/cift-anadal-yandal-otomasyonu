using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace CapYandal.Web.Services.Belgeler;

// Yalnız genel DOCX paket doğrulaması; antivirüs veya zararsızlık garantisi değildir.
public static class DocxPaketKurali
{
    private const long ToplamSinir = 50 * 1024 * 1024;
    private const long ParcaSiniri = 20 * 1024 * 1024;
    private const string WordIcerik = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    public static bool Gecerli(Stream akis, CancellationToken iptal = default)
    {
        ArgumentNullException.ThrowIfNull(akis);
        // Limitli sunucu kopyası seekable olmalı; kullanıcı dosya adına bakılmaz.
        if (!akis.CanRead || !akis.CanSeek || akis.Length <= 0 ||
            akis.Length > BelgeSurumKurali.DosyaSiniri) return false;
        akis.Position = 0;
        try
        {
            using var arsiv = new ZipArchive(akis, ZipArchiveMode.Read, leaveOpen: true);
            if (arsiv.Entries.Count is 0 or > 200) return false;
            var adlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long toplam = 0;
            byte[] tampon = new byte[8192];
            foreach (var parca in arsiv.Entries)
            {
                iptal.ThrowIfCancellationRequested();
                var ad = parca.FullName;
                if (!adlar.Add(ad) || ad.StartsWith('/') || ad.Contains('\\') ||
                    ad.Contains(':') || ad.Split('/').Any(s => s is ".." or ".") ||
                    ad.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                    parca.Length > ParcaSiniri) return false;
                // Merkezi dizin boyut beyanı tek başına yeterli değildir.
                using var acilan = parca.Open();
                long okunan = 0;
                int adet;
                while ((adet = acilan.Read(tampon, 0, tampon.Length)) != 0)
                {
                    iptal.ThrowIfCancellationRequested();
                    okunan += adet; toplam += adet;
                    if (okunan > ParcaSiniri || toplam > ToplamSinir) return false;
                }
                if (okunan != parca.Length) return false;
                if (ad.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    ad.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    using var xmlAkis = parca.Open();
                    using var xmlOkuyucu = XmlReader.Create(xmlAkis, Ayarlar(ParcaSiniri));
                    while (xmlOkuyucu.Read()) iptal.ThrowIfCancellationRequested();
                }
            }
            var turler = arsiv.GetEntry("[Content_Types].xml");
            var belge = arsiv.GetEntry("word/document.xml");
            if (turler is null || belge is null || turler.Length > 1024 * 1024) return false;
            var xml = Oku(turler, 1024 * 1024);
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (xml.Root?.Name != ct + "Types") return false;
            var tanimlar = xml.Root.Elements().ToArray();
            if (tanimlar.Any(e => ((string?)e.Attribute("ContentType")) is { } tip &&
                (tip.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) ||
                 tip.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))) return false;
            var ana = tanimlar.Where(e => e.Name == ct + "Override" &&
                (string?)e.Attribute("PartName") == "/word/document.xml").ToArray();
            if (ana.Length != 1 || (string?)ana[0].Attribute("ContentType") != WordIcerik)
                return false;
            var doc = Oku(belge, ParcaSiniri);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            return doc.Root?.Name == w + "document" && doc.Root.Element(w + "body") is not null;
        }
        catch (InvalidDataException) { return false; }
        catch (XmlException) { return false; }
        // İptal ve IO hataları biçim hatası gibi yutulmaz; servis ayrı ele alır.
    }

    private static XDocument Oku(ZipArchiveEntry parca, long sinir)
    {
        using var akis = parca.Open();
        using var okuyucu = XmlReader.Create(akis, Ayarlar(sinir));
        return XDocument.Load(okuyucu);
    }

    private static XmlReaderSettings Ayarlar(long sinir) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
        MaxCharactersInDocument = sinir, MaxCharactersFromEntities = 0
    };
}

