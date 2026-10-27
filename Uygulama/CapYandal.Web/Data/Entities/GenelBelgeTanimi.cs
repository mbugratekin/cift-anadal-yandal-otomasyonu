namespace CapYandal.Web.Data.Entities;

// Kategori aynı kalsa da her form bağımsız kimlik ve sürüm serisi taşır.
public sealed class GenelBelgeTanimi
{
    public Guid Id { get; set; }
    public string Tur { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
