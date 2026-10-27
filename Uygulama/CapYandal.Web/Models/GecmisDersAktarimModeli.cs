using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

// İlk aktarım: katalog kimlikleri alınır ders değerleri ve aktör sunucuda çözülür.
public sealed class GecmisDersAktarimModeli : IValidatableObject
{
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    [Required, StringLength(16)] public string ProgramSurum { get; set; } = "";
    [Required, StringLength(16)] public string DonemSurum { get; set; } = "";
    [Required, StringLength(16)] public string SablonSurum { get; set; } = "";
    [Required, StringLength(2000)] public string Gerekce { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)] public List<GecmisDersSatiri> Satirlar { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProgramId == Guid.Empty || DonemId == Guid.Empty || string.IsNullOrWhiteSpace(Gerekce))
            yield return new("Program, dönem ve dayanak gerekçesi gerekli.");
        if (Satirlar is null || Satirlar.Count is < 1 or > 100) yield break;
        if (Satirlar.Any(x => x is null || !Validator.TryValidateObject(x, new(x), new List<ValidationResult>(), true)))
            yield return new("Ders, not ve geçilen dönem bilgilerini kontrol edin.");
        if (Satirlar.Where(x => x is not null).Select(x => x.DersId).Distinct().Count() != Satirlar.Count)
            yield return new("Aynı ders bu döneme birden fazla kez aktarılamaz.");
    }
}

public sealed class GecmisDersSatiri : IValidatableObject
{
    public Guid DersId { get; set; }
    public DersDenemeDurumu Durum { get; set; } = DersDenemeDurumu.Alindi;
    [StringLength(32)] public string? Not { get; set; }
    [Range(0,100), Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(SayisalNotModelBinder))] public decimal? SayisalNot { get; set; }
    public Guid? GecilenDonemId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DersId == Guid.Empty || !Enum.IsDefined(Durum)) yield return new("Katalogdan ders ve geçerli durum seçin.");
        if (!CapYandal.Web.Services.DersKaydi.HarfNotuKurali.Gecerli(Not)) yield return new("Transkriptteki harf notunu seçin.");
        if (Not == "M" && Durum != DersDenemeDurumu.Muaf)
            yield return new("M notu için Muaf durumunu seçin.");
        if ((Durum == DersDenemeDurumu.Gecti && (GecilenDonemId is null || GecilenDonemId == Guid.Empty)) ||
            (Durum != DersDenemeDurumu.Gecti && GecilenDonemId is not null))
            yield return new("Geçilen dönem yalnız geçti durumunda gerekli.");
    }
}

public sealed record GecmisAktarimSecenekleri(string ProgramSurum, string SablonSurum,
    IReadOnlyList<GecmisAktarimDonemi> Donemler, IReadOnlyList<GecmisAktarimDersi> Dersler);
public sealed record GecmisAktarimDonemi(Guid Id, string Ad, string Surum);
public sealed record GecmisAktarimDersi(Guid Id, string Kod, string Ad);
