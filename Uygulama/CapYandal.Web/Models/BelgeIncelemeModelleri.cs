using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

public sealed class EkBelgeIsteModeli : IValidatableObject
{
    public Guid IstekId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public KisiselBelgeTuru? Tur { get; set; }
    [StringLength(2000)] public string? Metin { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (IstekId == Guid.Empty || ProgramId == Guid.Empty || DonemId == Guid.Empty) yield return new("Program ve dönemi seçin.");
        if (Tur is null || !Enum.IsDefined(Tur.Value)) yield return new("Belge türünü seçin.", [nameof(Tur)]);
    }
}

public sealed class BelgeKararModeli : IValidatableObject
{
    public Guid ProgramId { get; set; }
    public Guid BelgeId { get; set; }
    public BelgeOnayDurumu? Karar { get; set; }
    [StringLength(2000)] public string? Gerekce { get; set; }
    public byte[] Surum { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProgramId == Guid.Empty || BelgeId == Guid.Empty) yield return new("Belge ve programı seçin.");
        if (Karar is not (BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.Reddedildi or BelgeOnayDurumu.IptalEdildi)) yield return new("Geçerli karar seçin.", [nameof(Karar)]);
        if (Karar is BelgeOnayDurumu.Reddedildi or BelgeOnayDurumu.IptalEdildi && string.IsNullOrWhiteSpace(Gerekce)) yield return new("Ret veya iptal gerekçesini yazın.", [nameof(Gerekce)]);
        if (Surum is null || Surum.Length != 8) yield return new("Güncel belge sürümü gerekli.", [nameof(Surum)]);
    }
}
