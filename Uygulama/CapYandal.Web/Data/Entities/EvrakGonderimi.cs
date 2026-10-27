namespace CapYandal.Web.Data.Entities;

public class EvrakGonderimi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid GonderenKullaniciId { get; set; }
    public DateTimeOffset GonderimAni { get; set; }
}
