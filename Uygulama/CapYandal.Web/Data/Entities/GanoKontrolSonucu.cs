namespace CapYandal.Web.Data.Entities;

public class GanoKontrolSonucu
{
    public Guid Id { get; set; }
    public Guid GanoKaydiId { get; set; }
    public Guid KosulKumesiId { get; set; }
    public string KuralKodu { get; set; } = string.Empty;
    // Eski sonuç kopyaları bilinmiyorsa boş kalır; bugünkü değerlerle doldurulmaz.
    public int? RevizyonNo { get; set; }
    public decimal? GanoKopyasi { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(500)] public string? KaynakKopyasi { get; set; }
    public GanoSonucKodu SonucKodu { get; set; }
    public GanoSonucRengi? Renk { get; set; }
    public bool GecmisEksikMi { get; set; }
    public decimal? KullanilanNormalEsik { get; set; }
    public decimal? KullanilanAltEsik { get; set; }
    public int? KullanilanIzinSayisi { get; set; }
    public int? KullanilanDusukDonemSayisi { get; set; }
    public Guid KontrolEdenKullaniciId { get; set; }
    public DateTimeOffset KontrolAni { get; set; }
}
