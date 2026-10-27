namespace CapYandal.Web.Data.Entities;

public class SablonDonemDersi
{
    public Guid SablonId { get; set; }
    public Guid DonemId { get; set; }
    public Guid DersId { get; set; }
    public bool SecimeAcikMi { get; set; }
    public string? OgretimElemani { get; set; }
    public Guid? DegistirenKullaniciId { get; set; }
    public DateTimeOffset? DegisiklikAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
