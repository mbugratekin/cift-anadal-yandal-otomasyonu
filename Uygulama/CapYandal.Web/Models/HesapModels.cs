using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class GirisModel
{
    [Required(ErrorMessage = "Kullanıcı adını yazın.")]
    [StringLength(256, ErrorMessage = "Kullanıcı adı çok uzun.")]
    [BoslukIcermez("Kullanıcı adında")]
    [Display(Name = "Kullanıcı adı")]
    public string KullaniciAdi { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parolanızı yazın.")]
    [StringLength(1024, ErrorMessage = "Parola çok uzun.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Parola { get; set; } = string.Empty;
    public string? DonusAdresi { get; set; }
}

public sealed class ParolaDegistirModel
{
    [Required(ErrorMessage = "Mevcut parolanızı yazın.")]
    [StringLength(1024, ErrorMessage = "Mevcut parola çok uzun.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string MevcutParola { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolanızı yazın.")]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "Parola 8–20 karakter arasında olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string YeniParola { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolanızı tekrar yazın.")]
    [Compare(nameof(YeniParola), ErrorMessage = "Yeni parolalar aynı olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola tekrar")]
    public string YeniParolaTekrar { get; set; } = string.Empty;
}

public sealed class SifreSifirlamaTalebiModel
{
    [Required(ErrorMessage = "Kullanıcı adınızı yazın.")]
    [StringLength(256, ErrorMessage = "Kullanıcı adı çok uzun.")]
    [BoslukIcermez("Kullanıcı adında")]
    [Display(Name = "Kullanıcı adı / Öğrenci numarası")]
    public string KullaniciAdi { get; set; } = string.Empty;
}

public sealed class HesapKurulumModel
{
    [Required(ErrorMessage = "Kişisel e-posta adresinizi yazın.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")]
    [StringLength(256, ErrorMessage = "E-posta adresi çok uzun.")]
    [Display(Name = "Kişisel e-posta")]
    public string KisiselEposta { get; set; } = string.Empty;
}

public sealed class KisiselEpostaDogrulamaModel
{
    [Required]
    public Guid UserId { get; set; }
    [Required, StringLength(32, MinimumLength = 32)]
    public string RequestId { get; set; } = string.Empty;
    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? Adres { get; set; }
}

public sealed class SifreSifirlamaModel
{
    [Required]
    public Guid UserId { get; set; }

    [Required, StringLength(32, MinimumLength = 32)]
    public string ResetId { get; set; } = string.Empty;

    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolanızı yazın.")]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "Parola 8–20 karakter arasında olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string YeniParola { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolanızı tekrar yazın.")]
    [Compare(nameof(YeniParola), ErrorMessage = "Yeni parolalar aynı olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola tekrar")]
    public string YeniParolaTekrar { get; set; } = string.Empty;
}
