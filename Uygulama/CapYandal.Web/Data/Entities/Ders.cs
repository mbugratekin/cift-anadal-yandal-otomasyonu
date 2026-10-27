namespace CapYandal.Web.Data.Entities;

public class Ders
{
    public Guid Id { get; set; }
    public OgrenciProgramTuru Tur { get; set; }
    public string HedefBolum { get; set; } = string.Empty;
    public string? KaynakAnaDal { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public decimal Akts { get; set; }
    public bool EtkinMi { get; set; } = true;
}
