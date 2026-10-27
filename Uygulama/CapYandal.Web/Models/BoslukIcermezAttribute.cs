using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

// Kullanıcı adı ve parola gibi bitişik yazılması gereken alanlarda kullanılır.
public sealed class BoslukIcermezAttribute : ValidationAttribute
{
    public BoslukIcermezAttribute(string alanAdi = "Parolada")
        : base($"{alanAdi} boşluk kullanılamaz.") { }

    public override bool IsValid(object? value) =>
        value is not string text || !text.Any(char.IsWhiteSpace);
}
