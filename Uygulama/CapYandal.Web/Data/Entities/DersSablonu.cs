namespace CapYandal.Web.Data.Entities;

public class DersSablonu
{
    public Guid Id { get; set; }
    public Guid? MufredatId { get; set; }
    public OgrenciProgramTuru Tur { get; set; }
    public string HedefBolum { get; set; } = string.Empty;
    public string? KaynakAnaDal { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int MufredatNo { get; set; } = 1;
    public string? MufredatAdi { get; set; }
    public bool EtkinMi { get; set; } = true;
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturulmaAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
