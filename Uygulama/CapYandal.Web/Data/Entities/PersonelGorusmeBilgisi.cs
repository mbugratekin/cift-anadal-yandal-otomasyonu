namespace CapYandal.Web.Data.Entities;

public sealed class PersonelGorusmeBilgisi
{
    public Guid KullaniciId { get; set; }
    public string Blok { get; set; } = "";
    public string Oda { get; set; } = "";
    // Yalnız sohbet iletişim adresidir; giriş/kurtarma e-postasını değiştirmez.
    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string? IletisimEpostasi { get; set; }
    public string AraliklarJson { get; set; } = "[]";
    public DateTimeOffset DegisiklikAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
