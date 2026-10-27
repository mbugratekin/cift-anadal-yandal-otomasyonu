using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;
public sealed record KayitKapiPanelModel(int Yil, int Yariyil, string Surum, bool AcikMi, string Baslangic, string Bitis);
public sealed class KayitKapisiModel
{
    [Range(2, 9998)] public int Yil { get; set; }
    [Range(1, 2)] public int Yariyil { get; set; }
    [Range(1, 2)] public int Niyet { get; set; }
    [StringLength(16)] public string? Surum { get; set; }
    [StringLength(16), DisplayFormat(ConvertEmptyStringToNull = false)] public string? Baslangic { get; set; }
    [StringLength(16), DisplayFormat(ConvertEmptyStringToNull = false)] public string? Bitis { get; set; }
}
