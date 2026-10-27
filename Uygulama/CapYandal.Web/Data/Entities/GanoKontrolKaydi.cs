namespace CapYandal.Web.Data.Entities;

public class GanoKontrolKaydi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public GanoKapsami Kapsam { get; set; }
    public decimal Gano { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(500)] public string? Kaynak { get; set; }
    public Guid GirenKullaniciId { get; set; }
    public Guid? DuzeltenKullaniciId { get; set; }
    public DateTimeOffset DegisiklikAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
