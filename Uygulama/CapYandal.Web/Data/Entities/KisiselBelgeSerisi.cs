namespace CapYandal.Web.Data.Entities;

// Arşiv serisi — tür, öğrenci programı ve dönem aynı belge sürümlerinde sabit kalır.
public class KisiselBelgeSerisi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public KisiselBelgeTuru Tur { get; set; }
    public bool OgrenciKilidi { get; set; }
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturulmaAni { get; set; }
}
