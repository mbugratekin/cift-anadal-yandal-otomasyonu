using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class ProgramTakipNotuEkleModeli : IValidatableObject
{
    public Guid ProgramId { get; set; }

    [Required(ErrorMessage = "Not metnini yazın.")]
    [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
    public string Metin { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProgramId == Guid.Empty)
            yield return new ValidationResult("Programı seçin.", [nameof(ProgramId)]);
    }
}
