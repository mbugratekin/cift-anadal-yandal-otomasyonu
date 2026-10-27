using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

// Öğrenci kaydı: form biçimi doğrulanır yetki ve atomik hesap kaydı servistedir.
public sealed class OgrenciKayitModel : IValidatableObject
{
    private string ad = string.Empty;
    private string soyad = string.Empty;
    private string kurumsalEposta = string.Empty;
    private string kaynakAnaDal = string.Empty;

    [Required(ErrorMessage = "Öğrenci numarasını yazın.")]
    [StringLength(9, MinimumLength = 9, ErrorMessage = "Öğrenci numarası 9 rakam olmalıdır!")]
    [RegularExpression("^[0-9]{9}$", ErrorMessage = "Öğrenci numarası 9 rakam olmalıdır!")]
    [Display(Name = "Öğrenci numarası")]
    public string OgrenciNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adı yazın."), StringLength(100)]
    [Display(Name = "Ad")]
    public string Ad { get => ad; set => ad = value?.Trim() ?? string.Empty; }

    [Required(ErrorMessage = "Soyadı yazın."), StringLength(100)]
    [Display(Name = "Soyad")]
    public string Soyad { get => soyad; set => soyad = value?.Trim() ?? string.Empty; }

    // Öğrenci adresi kullanıcıdan istenmez; öğrenci numarasından sunucuda üretilir.
    public string KurumsalEposta { get => kurumsalEposta; set => kurumsalEposta = value?.Trim() ?? string.Empty; }

    [Required(ErrorMessage = "Ana dalı yazın.")]
    [StringLength(128, ErrorMessage = "Ana dal en fazla 128 karakter olabilir.")]
    [Display(Name = "Ana dal")]
    public string KaynakAnaDal { get => kaynakAnaDal; set => kaynakAnaDal = value?.Trim() ?? string.Empty; }

    [Required(ErrorMessage = "Program türünü seçin.")]
    [EnumDataType(typeof(OgrenciProgramTuru), ErrorMessage = "ÇAP veya Yan Dal seçin.")]
    [Display(Name = "Program türü")]
    public OgrenciProgramTuru? Tur { get; set; }

    [Display(Name = "Müfredat")]
    public Guid? MufredatSablonId { get; set; }

    [Required(ErrorMessage = "Kabulün akademik yıl başlangıcını yazın."), Range(1900, 9998, ErrorMessage = "Geçerli bir dört haneli kabul yılı yazın!")]
    [Display(Name = "Kabul akademik yılı başlangıcı")]
    public int? KabulAkademikYilBaslangici { get; set; }

    [Required(ErrorMessage = "Kabul dönemini seçin."), EnumDataType(typeof(AkademikYariyil))]
    [Display(Name = "Kabul dönemi")]
    public AkademikYariyil? KabulYariyili { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KabulAkademikYilBaslangici > DateTime.UtcNow.Year + 1)
            yield return new ValidationResult("Kabul yılı gelecek akademik yıldan ileri olamaz!", [nameof(KabulAkademikYilBaslangici)]);
        if (Tur is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal))
            yield return new ValidationResult("ÇAP veya Yan Dal seçin.", [nameof(Tur)]);
    }
}
