using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class ProgramTakipNotuSilModeli : IValidatableObject
{
    public Guid NotId { get; set; }
    public Guid ProgramId { get; set; }
    public byte[] Surum { get; set; } = [];
    [Required, StringLength(2000)] public string Gerekce { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NotId == Guid.Empty) yield return new ValidationResult("Not seçilmelidir.", [nameof(NotId)]);
        if (ProgramId == Guid.Empty) yield return new ValidationResult("Programı seçin.", [nameof(ProgramId)]);
        if (string.IsNullOrWhiteSpace(Gerekce)) yield return new ValidationResult("İptal gerekçesini yazın.", [nameof(Gerekce)]);
        if (Surum is null || Surum.Length != 8)
            yield return new ValidationResult("Geçerli sürüm bilgisi gereklidir.", [nameof(Surum)]);
    }
}
