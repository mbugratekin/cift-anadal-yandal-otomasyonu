namespace CapYandal.Web.Models;

public sealed record GanoGorunumSatiri(int Yil, string Yariyil,
    Data.Entities.GanoKapsami Kapsam, decimal Gano, Data.Entities.GanoSonucRengi? Renk, string Durum)
{
    public string RenkSinifi => Renk switch { Data.Entities.GanoSonucRengi.Yesil => "gano-normal",
        Data.Entities.GanoSonucRengi.Sari => "gano-allowed", Data.Entities.GanoSonucRengi.Kirmizi => "gano-violation", _ => "gano-neutral" };
}

public sealed record ProgramOzetModel(string Tur, string KaynakAnaDal);
public sealed record PersonelGorevSecenegi(Guid Id, string AdSoyad, string Rol)
{
    public bool GorevliMi { get; init; } = true;
    public bool HazirMi { get; init; } = true;
}
public sealed record OgrenciSatirModel(Guid ProgramId, string OgrenciNo, string AdSoyad, string Tur, string KaynakAnaDal,
    bool AktifMi);
public sealed record GanoSatirModel(int Yil, string Yariyil, decimal? AnaDalGano, decimal? ProgramGano)
{
    public string? AnaDalSurum { get; init; }
    public string? AnaDalKaynak { get; init; }
    public string? ProgramSurum { get; init; }
}
public sealed record ProgramTakipNotuSatirModel(Guid Id, string Metin, DateTimeOffset OlusturmaAni, string Surum);
public sealed record OgrenciAyrintiModel(Guid ProgramId, string OgrenciNo, string AdSoyad, string? KurumsalEposta,
    string? KisiselEposta, bool KisiselEpostaOnayliMi, string AnaDal, string Program, bool AktifMi,
    bool FotografVarMi, IReadOnlyList<GanoSatirModel> GanoKayitlari)
{
    public bool IlkParolaDavetineUygunMu { get; init; }
    public int? KabulAkademikYilBaslangici { get; init; }
    public Data.Entities.AkademikYariyil? KabulYariyili { get; init; }
    public string Mufredat { get; init; } = "Atanmamış";
    public bool MufredatAktifMi { get; init; }
    public string DersListesi { get; init; } = "Atanmamış";
    public string? MufredatKapsami { get; init; }
    public Data.Entities.ProgramKararTuru? SonKarar { get; init; }
    public string? KararGerekcesi { get; init; }
    public DateTimeOffset? KararAni { get; init; }
    public string Surum { get; init; } = "";
}
public sealed record SablonDersSatirModel(string Kod, string Ad, decimal Akts)
{
    public decimal? Kredi { get; init; }
    public Guid DersId { get; init; }
    public int MufredatYariyili { get; init; }
    public int Tur { get; init; }
}
public sealed record SablonSatirModel(string Ad, string Program, string? AnaDal)
{
    public Guid? OrtakMufredatId { get; init; }
    public int MufredatNo { get; init; } = 1;
    public string? MufredatAdi { get; init; }
    public bool EtkinMi { get; init; } = true;
    public IReadOnlyList<SablonDersSatirModel> Dersler { get; init; } = [];
    public Guid Id { get; init; }
    public string Surum { get; init; } = "";
}

public sealed record PanelModel(string Baslik, IReadOnlyList<string> Bolumler)
{
    public IReadOnlyList<PersonelGorevSecenegi> PersonelGorevlileri { get; init; } = [];
    public string KullaniciAdi { get; init; } = string.Empty;
    public string? OgrenciNo { get; init; }
    public string? KurumsalEposta { get; init; }
    public string? KisiselEposta { get; init; }
    public bool KisiselEpostaDogrulandiMi { get; init; }
    public bool FotografVarMi { get; init; }
    public bool OgrenciMi { get; init; }
    public bool OgrenciDersMenuleriGorunur { get; init; } = true;
    public IReadOnlyList<ProgramOzetModel> Programlar { get; init; } = [];
    public string AktifBolum { get; init; } = "Ana Sayfa";
    public IReadOnlyList<OgrenciSatirModel> Ogrenciler { get; init; } = [];
    public string? Arama { get; init; }
    public string? ProgramTuru { get; init; }
    public string? ProgramDurumu { get; init; }
    public OgrenciAyrintiModel? OgrenciAyrintisi { get; init; }
    public IReadOnlyList<ProgramTakipNotuSatirModel> TakipNotlari { get; init; } = [];
    public DonemYonetimiPaneli? DonemIzinleri { get; init; }
    public IReadOnlyList<SablonSatirModel> Sablonlar { get; init; } = [];
    public IReadOnlyList<MufredatSatiri> Mufredatlar { get; init; } = [];
    public IReadOnlyList<GenelBelgeSatirModel> GenelBelgeler { get; init; } = [];
    public KayitKapiPanelModel? KayitKapisi { get; init; }
    public DersPanelModel? DersPaneli { get; init; }
    public EvrakSayfaModel? EvrakPaneli { get; init; }
    public SohbetPanelModel? SohbetPaneli { get; init; }
    public IReadOnlyList<GorusmeBilgisiModel> GorusmeBilgileri { get; init; } = [];
    public GorusmeBilgisiModel? KendiGorusmeBilgisi { get; init; }
    public string? OnerilenGanoDonemi { get; init; }
    public IReadOnlyList<GanoGorunumSatiri> GanoGorunumu { get; init; } = [];
    public string? GanoOkumaHatasi { get; init; }
    public DenetimSayfaModeli? DenetimGecmisi { get; init; }
}
