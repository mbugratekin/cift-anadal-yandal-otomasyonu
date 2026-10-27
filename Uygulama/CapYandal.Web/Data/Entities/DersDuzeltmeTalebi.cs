namespace CapYandal.Web.Data.Entities;

// Öğrencinin değiştirmesi istenen özgün ders kopyası; kapandıktan sonra da saklanır.
public class DersDuzeltmeTalebi
{
    public Guid Id { get; set; }
    public Guid ListeId { get; set; }
    public Guid KalemId { get; set; }
    public Guid IsteyenKullaniciId { get; set; }
    public DateTimeOffset IstekAni { get; set; }
    public string Gerekce { get; set; } = "";
    public Guid? KapatanKullaniciId { get; set; }
    public DateTimeOffset? KapatilmaAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
