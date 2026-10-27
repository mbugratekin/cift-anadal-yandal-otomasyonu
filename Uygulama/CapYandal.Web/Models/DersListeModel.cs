using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed record DersGorunumSatiri(Guid DersId, string Kod, string Ad, decimal? Kredi, decimal Akts, int? Tur, int? Yariyil);
public sealed record DersPanelListesi(Guid ProgramId, Guid? ListeId, Guid DonemId, string Ogrenci, string MufredatAdi,
    string? SablonSurum, string? ListeSurum, bool Onayli, bool Duzenlenebilir,
    IReadOnlyList<DersGorunumSatiri> Kalemler, IReadOnlyList<DersGorunumSatiri> Secenekler, IReadOnlyList<DersIptalSatiri>? IptalKalemleri = null,
    IReadOnlyList<DersDenemeSatiri>? Denemeler = null, CapYandal.Web.Data.Entities.DersSecimiDurumu? Durum = null, string? KararGerekcesi = null,
    IReadOnlyList<DersListeGecmisSatiri>? ListeGecmisi = null, bool GecmisAktarimiMi = false, DersIlerlemeModeli? Ilerleme = null)
{
    public bool MazeretliMi { get; init; }
    public bool ProgramAktifMi { get; init; } = true;
    public IReadOnlyList<Guid> DuzeltilecekDersler { get; init; } = [];
    public IReadOnlyList<DersEklemeSatiri> EklenenKalemler { get; init; } = [];
    public string? DegisiklikGecmisiHatasi { get; init; }
}
public sealed record DersEklemeSatiri(string Kod, string Ad, DateTimeOffset Ani, string? Gerekce);
public sealed record DersListeGecmisSatiri(int SurumNo, CapYandal.Web.Data.Entities.DersSecimiDurumu Durum, string? Gerekce, DateTimeOffset Ani, bool EskiKayitKopyasi);
public sealed record DersDenemeSatiri(Guid KalemId, string Kod, string Ad, Guid? DenemeId, int SurumNo,
    string? Not, CapYandal.Web.Data.Entities.DersDenemeDurumu Durum, CapYandal.Web.Data.Entities.KayitOnayDurumu OnayDurumu,
    Guid? GecilenDonemId, bool Duzenlenebilir, IReadOnlyList<DersDenemeGecmisSatiri> Gecmis)
{ public decimal? SayisalNot { get; init; } }
public sealed record DersDenemeGecmisSatiri(int SurumNo, string? Not, CapYandal.Web.Data.Entities.DersDenemeDurumu Durum,
    CapYandal.Web.Data.Entities.KayitOnayDurumu OnayDurumu, string? Gerekce, DateTimeOffset Ani, string? GecilenDonem)
{ public decimal? SayisalNot { get; init; } }
public sealed record DersDenemePaneli(Guid ProgramId, Guid DonemId, bool Koordinator, IReadOnlyList<DersDenemeSatiri> Satirlar,
    IReadOnlyList<DersGorunumSatiri>? Dersler = null);
public sealed record DersIptalSatiri(string Kod, string Ad, DateTimeOffset Ani, string Gerekce);
public sealed record DersPanelModel(int Yil, int Yariyil, bool KayitAcik, IReadOnlyList<DersPanelListesi> Listeler);
public sealed record DersTabloModel(IReadOnlyList<DersGorunumSatiri> Satirlar, bool Duzenlenebilir, bool Onayli, bool Sonuc,
    string? DurumMetni = null, IReadOnlyList<Guid>? DuzeltilecekDersler = null, string? KararFormu = null);
public sealed record DersSatirModel(DersGorunumSatiri Ders, bool Duzenlenebilir, bool Onayli, bool Sonuc,
    string? DurumMetni = null, string? KararFormu = null);

public sealed class DersListeKomutModel
{
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid ListeId { get; set; }
    [StringLength(16)] public string? ListeSurum { get; set; }
    [StringLength(16)] public string? SablonSurum { get; set; }
    public Guid[]? Dersler { get; set; }
    public Guid[]? DuzeltilecekDersler { get; set; }
    [StringLength(2000)] public string? Gerekce { get; set; }
    public CapYandal.Web.Data.Entities.DersSecimiDurumu? Karar { get; set; }
    [Range(2, 9998)] public int Yil { get; set; }
    [Range(1, 2)] public int Yariyil { get; set; }
}
