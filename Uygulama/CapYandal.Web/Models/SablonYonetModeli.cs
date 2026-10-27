using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class SablonEkleModeli
{
    [Range(1, 2)] public int Tur { get; set; }
    [Range(1, int.MaxValue)] public int MufredatNo { get; set; } = 1;
    [StringLength(128)] public string? KaynakAnaDal { get; set; }
    [StringLength(100)] public string? MufredatAdi { get; set; }
}

public sealed class SablonDurumModeli
{
    public Guid SablonId { get; set; }
    [Required, StringLength(16)] public string Surum { get; set; } = "";
    [Required] public bool? EtkinMi { get; set; }
}

public sealed class MufredatAdModeli
{
    public Guid SablonId { get; set; }
    [Required, StringLength(16)] public string Surum { get; set; } = "";
    [Required, StringLength(100)] public string MufredatAdi { get; set; } = "";
}
