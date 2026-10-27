using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class DonemIzniModel
{
    public Guid ProgramId { get; set; }
    public Guid? Id { get; set; }
    public string? Surum { get; set; }
    [Range(1, 9998)] public int BaslangicYili { get; set; }
    [Range(1, 2)] public int BaslangicYariyili { get; set; }
    [Range(1, 9998)] public int BitisYili { get; set; }
    [Range(1, 2)] public int BitisYariyili { get; set; }
    public string Tur { get; set; } = "Dondurma";
    [Required, StringLength(2000)] public string Gerekce { get; set; } = "";
    public Guid? BelgeId { get; set; }
}
public sealed record DonemIzniSatiri(Guid Id, string Baslangic, string Bitis, string Tur, string Gerekce,
    Guid? BelgeId, bool Iptal, string? IptalGerekcesi, string Surum);
public sealed record DonemYonetimiPaneli(Guid? ProgramId, IReadOnlyList<DonemIzniSatiri> Izinler,
    string? Durum = null)
{
    public IReadOnlyList<DonemBelgeSecenegi> Belgeler { get; init; } = [];
}
public sealed record DonemBelgeSecenegi(Guid Id, string Ad);
public sealed record IslemDonemiModel(Guid Id, string Ad, string Surum, string Sonraki);
