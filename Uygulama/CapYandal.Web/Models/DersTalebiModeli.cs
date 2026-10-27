using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

// Eksik ders bildirimi: katalog kaydı değil kalıcı sohbet mesajı hazırlanır.
public sealed class DersTalebiModeli : IValidatableObject
{
    public Guid IstekId { get; set; }
    [Required, StringLength(50)] public string DersKodu { get; set; } = "";
    [Required, StringLength(250)] public string DersAdi { get; set; } = "";
    [StringLength(1000)] public string? Aciklama { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(DersKodu) || string.IsNullOrWhiteSpace(DersAdi)) yield return new("Ders kodu ve adı boş bırakılamaz.");
        if (IstekId == Guid.Empty) yield return new("Geçerli istek kimliği gerekli.");
        if (DersKodu?.Any(char.IsControl) == true || DersAdi?.Any(char.IsControl) == true ||
            Aciklama?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            yield return new("Ders kodu ve adını tek satır, açıklamayı geçerli metin olarak yazın.");
    }

    public string MesajMetni() => $"Katalogda bulunmayan ders talebi\nDers kodu: {DersKodu.Trim()}\nDers adı: {DersAdi.Trim()}\nAçıklama: {Aciklama?.Trim()}";
}
