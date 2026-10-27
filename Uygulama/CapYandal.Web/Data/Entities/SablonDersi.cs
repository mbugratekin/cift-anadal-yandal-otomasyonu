namespace CapYandal.Web.Data.Entities;

public class SablonDersi
{
    public Guid SablonId { get; set; }
    public Guid DersId { get; set; }
    public int MufredatYariyili { get; set; }
    public DersTuru Tur { get; set; }
    public bool EtkinMi { get; set; } = true;
    // Katalog kimligi/FK korunur; degisiklik yalniz bu sablona aittir.
    public string? Kod { get; set; }
    public string? Ad { get; set; }
    public decimal? Akts { get; set; }
    public decimal? Kredi { get; set; }
}
