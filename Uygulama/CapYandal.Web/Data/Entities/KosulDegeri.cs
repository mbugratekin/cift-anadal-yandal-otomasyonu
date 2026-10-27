namespace CapYandal.Web.Data.Entities;

public class KosulDegeri
{
    public Guid Id { get; set; }
    public Guid KosulKumesiId { get; set; }
    public string KuralKodu { get; set; } = string.Empty;
    public Guid? KuralKimligi { get; set; }
    public GanoKontrolAmaci? Amac { get; set; }
    public GanoKontrolTuru? KontrolTuru { get; set; }
    public int? SiraNo { get; set; }
    public string? Aciklama { get; set; }
    public decimal? NormalEsik { get; set; }
    public decimal? AltEsik { get; set; }
    public decimal? ReferansDegeri { get; set; }
    public int? IzinSayisi { get; set; }
    public KosulIzinTuru? IzinTuru { get; set; }
    public int? ArdisikDonemSayisi { get; set; }
    public KosulDegerlendirmeTuru DegerlendirmeTuru { get; set; }
    public bool EtkinMi { get; set; } = true;
}
