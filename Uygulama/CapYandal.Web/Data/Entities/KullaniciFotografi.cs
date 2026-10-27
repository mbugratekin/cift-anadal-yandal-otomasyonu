namespace CapYandal.Web.Data.Entities;

public sealed class KullaniciFotografi
{
    public Guid KullaniciId { get; set; }
    public byte[] JpegIcerik { get; set; } = [];
    public DateTimeOffset OnayAni { get; set; }
}
