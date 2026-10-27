namespace CapYandal.Web.Data.Entities;

public class KosulKumesi
{
    public Guid Id { get; set; }
    // Mevcut koşul kümesi başka bir sürüme taşınmaz.
    public Guid? AkademikKosulSurumuId { get; set; }
    public OgrenciProgramTuru ProgramTuru { get; set; }
    public GanoKapsami Kapsam { get; set; }
    public DateOnly YururlukBaslangici { get; set; }
    public DateOnly? YururlukBitisi { get; set; }
    public bool EtkinMi { get; set; } = true;
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturulmaAni { get; set; }
}
