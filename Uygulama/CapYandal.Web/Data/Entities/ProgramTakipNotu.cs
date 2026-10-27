namespace CapYandal.Web.Data.Entities;

public class ProgramTakipNotu
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string Metin { get; set; } = string.Empty;
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturmaAni { get; set; }
    public Guid? DuzeltenKullaniciId { get; set; }
    public DateTimeOffset? DuzenlemeAni { get; set; }
    public bool SilindiMi { get; set; }
    public Guid? SilenKullaniciId { get; set; }
    public DateTimeOffset? SilmeAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
