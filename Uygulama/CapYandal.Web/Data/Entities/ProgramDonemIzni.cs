namespace CapYandal.Web.Data.Entities;

public class ProgramDonemIzni
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public int Baslangic { get; set; }
    public int Bitis { get; set; }
    public string Tur { get; set; } = "Izin";
    public string Gerekce { get; set; } = "";
    public Guid? BelgeId { get; set; }
    public Guid KaydedenId { get; set; }
    public DateTimeOffset KayitAni { get; set; }
    public string? IptalGerekcesi { get; set; }
    public Guid? IptalEdenId { get; set; }
    public DateTimeOffset? IptalAni { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
