using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class PersonelGorevAtaModeli : IValidatableObject
{
    public Guid KullaniciId { get; set; }
    [Required, StringLength(64)] public string Rol { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KullaniciId == Guid.Empty)
            yield return new("Hazırlanan hesabı seçin.", [nameof(KullaniciId)]);
        if (Rol is not (Security.UygulamaRolleri.Koordinator or Security.UygulamaRolleri.BolumBaskani))
            yield return new("Geçerli personel görevini seçin.", [nameof(Rol)]);
    }
}
