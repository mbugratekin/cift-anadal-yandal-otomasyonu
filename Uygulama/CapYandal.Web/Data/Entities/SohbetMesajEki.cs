namespace CapYandal.Web.Data.Entities;

// Mesaj ekinin bilgileri burada, dosyası özel depoda saklanır.
public sealed class SohbetMesajEki
{
    public Guid Id { get; set; }
    public Guid SohbetMesajiId { get; set; }
    public string DosyaAdi { get; set; } = "";
    public string DepolamaAnahtari { get; set; } = "";
    public string IcerikTuru { get; set; } = "";
    public long BoyutBayt { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset YuklemeAni { get; set; }
}
