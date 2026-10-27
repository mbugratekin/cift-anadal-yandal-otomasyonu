namespace CapYandal.Web.Data.Entities;

public class DegisiklikGecmisi
{
    public Guid Id { get; set; }
    public string VarlikTuru { get; set; } = string.Empty;
    public string VarlikAnahtari { get; set; } = string.Empty;
    public string Islem { get; set; } = string.Empty;
    public string? OncekiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public Guid? YapanKullaniciId { get; set; }
    public DateTimeOffset IslemAni { get; set; }
}
