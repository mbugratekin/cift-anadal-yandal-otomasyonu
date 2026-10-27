namespace CapYandal.Web.Models;

public sealed record DenetimDegisiklikModeli(string Alan, string Onceki, string Sonraki);

public sealed record DenetimSatirModeli(DateTimeOffset Zaman, string Yapan, string Rol, string Hedef,
    string Islem, string? Gerekce, IReadOnlyList<DenetimDegisiklikModeli> Degisiklikler,
    string Varlik, string Anahtar, string HamIslem, string? Onceki, string? Sonraki);
public sealed record DenetimSayfaModeli(int Sayfa, bool SonrakiVar, IReadOnlyList<DenetimSatirModeli> Kayitlar)
{
    public const int SayfaBoyutu = 20;
    public int ToplamKayit { get; init; }
    public int ToplamSayfa => Math.Max(1, (int)Math.Ceiling(ToplamKayit / (double)SayfaBoyutu));
}
