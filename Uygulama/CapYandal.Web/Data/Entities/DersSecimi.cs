namespace CapYandal.Web.Data.Entities;

public class DersSecimi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid SablonId { get; set; }
    public DateTimeOffset GonderimAni { get; set; }
    public Guid GonderenKullaniciId { get; set; }
    public DersSecimiDurumu Durum { get; set; } = DersSecimiDurumu.OnayBekliyor;
    public Guid? OnaylayanKullaniciId { get; set; }
    public DateTimeOffset? OnayAni { get; set; }
    public string? KararGerekcesi { get; set; }
    public bool OgrenciKilidi { get; set; }
    public bool GecmisAktarimiMi { get; set; }
    public bool MazeretliMi { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
