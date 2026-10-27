namespace CapYandal.Web.Data.Entities;

// Ortak sürüm. Programa özel ders listeleri DersSablonu kimliklerini korur.
public class Mufredat
{
    public Guid Id { get; set; }
    public string Ad { get; set; } = "";
    public int? BaslangicYili { get; set; } // Eski verinin başlangıcı bilinmiyorsa yalnız geçişte boş kalır.
    public bool EtkinMi { get; set; } = true;
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturmaAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
