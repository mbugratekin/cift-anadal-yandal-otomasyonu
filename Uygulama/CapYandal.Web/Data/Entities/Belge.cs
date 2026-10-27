namespace CapYandal.Web.Data.Entities;

public class Belge
{
    public Guid Id { get; set; }
    public BelgeKapsami Kapsam { get; set; }
    public string Tur { get; set; } = string.Empty;
    public Guid? GenelBelgeId { get; set; }
    public int? SurumNo { get; set; }
    public string? Sha256 { get; set; }
    public bool GenelArsivdeMi { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? DersSecimiId { get; set; }
    public Guid? EvrakGonderimiId { get; set; }
    public Guid? KisiselBelgeSerisiId { get; set; }
    public int? KisiselSurumNo { get; set; }
    public KisiselBelgeTuru? KisiselTur { get; set; }
    public Guid? ArsivDonemId { get; set; }
    public Guid? EkBelgeIstegiId { get; set; }
    // Eski belgede bilinmeyen inceleme sonucu null kalır; yeni yükleme beklemede başlar.
    public BelgeOnayDurumu? OnayDurumu { get; set; }
    public Guid? KararVerenKullaniciId { get; set; }
    public DateTimeOffset? KararAni { get; set; }
    public string? KararGerekcesi { get; set; }
    public Guid YukleyenKullaniciId { get; set; }
    public DateTimeOffset YuklemeAni { get; set; }
    public string? DosyaAdi { get; set; }
    public string? IcerikTuru { get; set; }
    public long? BoyutBayt { get; set; }
    public string? DepolamaAnahtari { get; set; }
    public string? ResmiBaglanti { get; set; }
    public BelgeIcerikDurumu IcerikDurumu { get; set; } = BelgeIcerikDurumu.Mevcut;
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset? IcerikKaldirilmaAni { get; set; }
    public Guid? KaldiranKullaniciId { get; set; }
}
