using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

// Program kararı: aktör ve zaman formdan alınmaz sürüm ve gerekçe zorunludur.
public sealed class ProgramKararModeli : IValidatableObject
{
    public Guid ProgramId { get; set; }
    public ProgramKararTuru? Karar { get; set; }
    [Required, StringLength(2000)]
    public string Gerekce { get; set; } = string.Empty;
    public byte[] Surum { get; set; } = [];
    // Yalnız ekrandaki aynı kararın ikinci gönderimi için kullanılır; yetki yerine geçmez.
    public bool UyariSonrasiTekrar { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProgramId == Guid.Empty) yield return new("Programı seçin.", [nameof(ProgramId)]);
        if (Karar is not (ProgramKararTuru.Mezuniyet or ProgramKararTuru.Sertifika or ProgramKararTuru.KayitKapatma))
            yield return new("Geçerli bir karar seçin.", [nameof(Karar)]);
        if (string.IsNullOrWhiteSpace(Gerekce)) yield return new("Karar gerekçesini yazın.", [nameof(Gerekce)]);
        if (Surum is null || Surum.Length != 8) yield return new("Sayfayı yenileyip tekrar deneyin.", [nameof(Surum)]);
    }
}
