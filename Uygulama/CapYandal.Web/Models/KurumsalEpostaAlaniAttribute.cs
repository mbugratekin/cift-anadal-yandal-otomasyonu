using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

// Sadece kurumsal kayıt alanlarına uygulanır kişisel iletişim adresine uygulanmaz.
[AttributeUsage(AttributeTargets.Property)]
public sealed class KurumsalEpostaAlaniAttribute : ValidationAttribute
{
    private readonly bool ogrenciAdresi;
    public KurumsalEpostaAlaniAttribute(bool ogrenciAdresi = false) : base(ogrenciAdresi
        ? "Öğrenci e-posta adresi @selcuk.edu.tr veya @ogr.selcuk.edu.tr ile bitmelidir."
        : "Kurumsal e-posta adresi @selcuk.edu.tr ile bitmelidir.") { this.ogrenciAdresi = ogrenciAdresi; }

    public override bool IsValid(object? value) => value is null ||
        value is string address && new EmailAddressAttribute().IsValid(address) &&
        (address.EndsWith("@selcuk.edu.tr", StringComparison.OrdinalIgnoreCase) ||
         ogrenciAdresi && address.EndsWith("@ogr.selcuk.edu.tr", StringComparison.OrdinalIgnoreCase));
}
