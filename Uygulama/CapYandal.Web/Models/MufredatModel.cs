using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed record MufredatSatiri(Guid Id, string Ad, int? BaslangicYili, bool EtkinMi, string Surum);
public sealed class MufredatEkleModel
{
    [Required, StringLength(100)] public string Ad { get; set; } = "";
    [Range(2, 9998)] public int BaslangicYili { get; set; }
}
public sealed class MufredatDurumModel
{
    public Guid Id { get; set; }
    [Required, StringLength(16)] public string Surum { get; set; } = "";
    public bool? EtkinMi { get; set; }
}
