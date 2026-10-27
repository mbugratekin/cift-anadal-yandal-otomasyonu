using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class ProgramTakipNotuDuzenleModeli : IValidatableObject
{
    public Guid NotId { get; set; }
    public Guid ProgramId { get; set; }

    [Required(ErrorMessage = "Not metnini yazın.")]
    [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
    public string Metin { get; set; } = string.Empty;

    public byte[] Surum { get; set; } = [];
    [Required, StringLength(2000)] public string Gerekce { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NotId == Guid.Empty)
            yield return new ValidationResult("Not seçilmelidir.", [nameof(NotId)]);

        if (ProgramId == Guid.Empty)
            yield return new ValidationResult("Programı seçin.", [nameof(ProgramId)]);

        if (string.IsNullOrWhiteSpace(Metin))
            yield return new ValidationResult("Not metni boşluktan ibaret olamaz.", [nameof(Metin)]);

        if (Surum is null || Surum.Length != 8)
            yield return new ValidationResult("Geçerli bir sürüm bilgisi gereklidir.", [nameof(Surum)]);
    }
}
