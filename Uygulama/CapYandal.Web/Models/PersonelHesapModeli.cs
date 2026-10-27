using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class PersonelHesapModeli
{
    [Required, StringLength(128)] public string Ad { get; set; } = "";
    [Required, StringLength(128)] public string Soyad { get; set; } = "";
    [Required, StringLength(256), BoslukIcermez("Kullanıcı adında")] public string KullaniciAdi { get; set; } = "";
    [Required, EmailAddress, StringLength(256), KurumsalEpostaAlani] public string KurumsalEposta { get; set; } = "";
    [Required, StringLength(64)] public string Rol { get; set; } = "";
}

public sealed class PersonelParolaOnayModeli
{
    public Guid UserId { get; set; }
    [Required, StringLength(32, MinimumLength=32)] public string RequestId { get; set; } = "";
    [Required, StringLength(4096)] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Yeni parolanızı yazın."), StringLength(20, MinimumLength = 8, ErrorMessage = "Parola 8–20 karakter arasında olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password), Display(Name = "Yeni parola")] public string YeniParola { get; set; } = "";
    [Required(ErrorMessage = "Yeni parolanızı tekrar yazın."), Compare(nameof(YeniParola), ErrorMessage = "Parolalar aynı olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password), Display(Name = "Yeni parola tekrar")] public string YeniParolaTekrar { get; set; } = "";
}
