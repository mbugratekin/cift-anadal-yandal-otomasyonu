using System.ComponentModel.DataAnnotations;

namespace CapYandal.Web.Models;

public sealed class PersonelDevirModeli
{
    public Guid EskiKullaniciId { get; set; }
    public Guid YeniKullaniciId { get; set; }
    [Required, StringLength(64)] public string Rol { get; set; } = "";
}
