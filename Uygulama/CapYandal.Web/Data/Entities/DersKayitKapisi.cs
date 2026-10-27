namespace CapYandal.Web.Data.Entities;

public sealed class DersKayitKapisi
{
    public Guid DonemId { get; set; }
    public bool AcikMi { get; set; }
    public DateTimeOffset? AcilisAniUtc { get; set; }
    public DateTimeOffset? KapanisAniUtc { get; set; }
    // Planı oluşturan gerçek kullanıcı saklanır.
    public Guid? PlanlayanKullaniciId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
