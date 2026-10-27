namespace CapYandal.Web.Data.Entities;

// Liste sürümü — yeniden gönderim önceki ret/iptal durumunu değiştirmeden yeni kopya oluşturur.
public sealed class DersListeSurumu
{
    public Guid Id { get; set; }
    public Guid ListeId { get; set; }
    public int SurumNo { get; set; }
    public DersSecimiDurumu Durum { get; set; }
    public string? Gerekce { get; set; }
    public string KopyaJson { get; set; } = "";
    public bool EskiKayitKopyasi { get; set; }
    public Guid YapanKullaniciId { get; set; }
    public DateTimeOffset IslemAni { get; set; }
}
