using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class GanoKaydetModeli
{
    public Guid ProgramId { get; set; }
    [Required, StringLength(12)] public string Donem { get; set; } = "";
    [StringLength(8)] public string? AnaDalGano { get; set; }
    [StringLength(500)] public string? AnaDalKaynak { get; set; }
    [StringLength(2000)] public string? Gerekce { get; set; }
    [StringLength(8)] public string? ProgramGano { get; set; }
    [StringLength(16)] public string? AnaDalSurum { get; set; }
    [StringLength(16)] public string? ProgramSurum { get; set; }
}

