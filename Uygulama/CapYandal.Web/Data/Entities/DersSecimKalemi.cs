namespace CapYandal.Web.Data.Entities;

public class DersSecimKalemi
{
    public Guid Id { get; set; }
    public Guid ListeId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid SablonId { get; set; }
    public Guid DersId { get; set; }
    public string DersKoduKopyasi { get; set; } = string.Empty;
    public string DersAdiKopyasi { get; set; } = string.Empty;
    public decimal AktsKopyasi { get; set; }
    public decimal? KrediKopyasi { get; set; }
    public DersTuru? TurKopyasi { get; set; }
    public int? MufredatYariyiliKopyasi { get; set; }
    public string? OgretimElemaniKopyasi { get; set; }
    public Guid EkleyenKullaniciId { get; set; }
    public DateTimeOffset EklenmeAni { get; set; }
    public Guid? DuzeltenKullaniciId { get; set; }
    public DateTimeOffset? DuzeltmeAni { get; set; }
    public string? DuzeltmeGerekcesi { get; set; }
    public DateTimeOffset? IptalAni { get; set; }
    public Guid? IptalEdenKullaniciId { get; set; }
    public string? IptalGerekcesi { get; set; }
}
