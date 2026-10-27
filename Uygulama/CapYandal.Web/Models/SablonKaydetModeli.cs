using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class SablonKaydetModeli
{
    public Guid SablonId { get; set; }
    [Required, StringLength(16)] public string Surum { get; set; } = "";
    public List<SablonDersGirdisi> Dersler { get; set; } = [];
    private string? mufredatAdi;
    [StringLength(100), DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? MufredatAdi
    {
        get => mufredatAdi;
        set { mufredatAdi = value; MufredatAdiGonderildi = true; }
    }
    // Alanın gönderilmemesi eski istemciyle uyumludur; boş gönderilmesi adı temizler.
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool MufredatAdiGonderildi { get; private set; }
}

public sealed class SablonDersGirdisi
{
    public Guid? DersId { get; set; }
    [StringLength(50)] public string? Kod { get; set; }
    [StringLength(250)] public string? Ad { get; set; }
    [StringLength(12)] public string? Akts { get; set; }
    [StringLength(12)] public string? Kredi { get; set; }
    public int MufredatYariyili { get; set; }
    public int Tur { get; set; }
    public bool Cikar { get; set; }
}
