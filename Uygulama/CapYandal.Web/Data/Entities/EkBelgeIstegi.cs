namespace CapYandal.Web.Data.Entities;

// Belge isteği — cevaplar belge arşivine bağlanır; eski istek ve cevaplar silinmez.
public class EkBelgeIstegi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public KisiselBelgeTuru BelgeTuru { get; set; }
    public string IstekMetni { get; set; } = string.Empty;
    public Guid IsteyenKullaniciId { get; set; }
    public DateTimeOffset IstekAni { get; set; }
    public EkBelgeIstegiDurumu Durum { get; set; } = EkBelgeIstegiDurumu.Bekliyor;
    public Guid? KararVerenKullaniciId { get; set; }
    public DateTimeOffset? KararAni { get; set; }
    public string? KararGerekcesi { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
