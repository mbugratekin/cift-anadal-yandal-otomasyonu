using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class IlkParolaModel
{
    public Guid UserId { get; set; }
    [Required(ErrorMessage = "Davet bağlantısı eksik."), StringLength(32, MinimumLength = 32, ErrorMessage = "Davet bağlantısı geçersiz.")]
    public string DavetId { get; set; } = string.Empty;
    [Required(ErrorMessage = "Davet bağlantısı eksik."), StringLength(4096, ErrorMessage = "Davet bağlantısı geçersiz.")]
    public string Token { get; set; } = string.Empty;
    [Required(ErrorMessage = "Parolanızı yazın."), StringLength(20, MinimumLength = 8, ErrorMessage = "Parola 8–20 karakter arasında olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password), Display(Name = "Parola")]
    public string YeniParola { get; set; } = string.Empty;
    [Required(ErrorMessage = "Parolanızı tekrar yazın."), Compare(nameof(YeniParola), ErrorMessage = "Parolalar aynı olmalıdır.")]
    [BoslukIcermez]
    [DataType(DataType.Password), Display(Name = "Parola tekrar")]
    public string YeniParolaTekrar { get; set; } = string.Empty;
}
