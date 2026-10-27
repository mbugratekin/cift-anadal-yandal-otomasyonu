using System.ComponentModel.DataAnnotations;
namespace CapYandal.Web.Models;

public sealed record SohbetOgrenciModel(Guid Id, string Ad)
{
    public int Okunmamis { get; init; }
    public Guid ProgramId { get; init; }
    public bool FotografVar { get; init; }
}
public sealed record SohbetMesajModel(Guid Id, string Gonderen, string Metin, DateTimeOffset Ani, bool BenimMi)
{
    public bool SabitlendiMi { get; init; }
    public SohbetEkModel? Ek { get; init; }
}
public sealed record SohbetEkModel(Guid Id, string DosyaAdi, long BoyutBayt);
public sealed record SohbetPanelModel(Guid? OgrenciId, bool KoordinatorMi, int Sayfa, bool EskiSayfaVar,
    IReadOnlyList<SohbetOgrenciModel> Ogrenciler, IReadOnlyList<SohbetMesajModel> Mesajlar);
public sealed class SohbetGonderModel
{
    public Guid IstekId { get; set; }
    public Guid OgrenciId { get; set; }
    [StringLength(2000)] public string? Metin { get; set; }
    public IFormFile? Ek { get; set; }
}
public sealed class SohbetOkunduModeli
{
    public Guid OgrenciId { get; set; }
    [MinLength(1), MaxLength(50)] public Guid[] Mesajlar { get; set; } = [];
}
