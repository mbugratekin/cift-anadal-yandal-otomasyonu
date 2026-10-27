namespace CapYandal.Web.Data.Entities;

public class SohbetMesaji
{
    public Guid Id { get; set; }
    public Guid OgrenciId { get; set; }
    public Guid GonderenKullaniciId { get; set; }
    public string Metin { get; set; } = string.Empty;
    public DateTimeOffset GonderimAni { get; set; }
    public DateTimeOffset? OkunmaAni { get; set; }
    // Önceki sürümden kalan kaldırma zamanı korunur; yeni yazışmalarda silme uygulanmaz.
    public DateTimeOffset? KaldirmaAni { get; set; }
    public bool SabitlendiMi { get; set; }
}
