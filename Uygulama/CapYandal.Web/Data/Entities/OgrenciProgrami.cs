namespace CapYandal.Web.Data.Entities;

public class OgrenciProgrami
{
    public Guid Id { get; set; }
    public Guid OgrenciId { get; set; }
    // Eski kayıtlara tahminle müfredat atanmaz.
    public Guid? MufredatSablonId { get; set; }
    public OgrenciProgramTuru Tur { get; set; }
    public string HedefBolum { get; set; } = string.Empty;
    public string KaynakAnaDal { get; set; } = string.Empty;
    public DateOnly BaslangicTarihi { get; set; }
    // Kabul dönemi — eski kayıtta bilinmeyen dönem null kalır; tarihten türetilmez.
    public int? KabulAkademikYilBaslangici { get; set; }
    public AkademikYariyil? KabulYariyili { get; set; }
    public bool AktifMi { get; set; } = true;
    public ProgramKararTuru? SonKarar { get; set; }
    public string? KararGerekcesi { get; set; }
    public Guid? KararVerenKullaniciId { get; set; }
    public DateTimeOffset? KararAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset DegisiklikAni { get; set; }
}
