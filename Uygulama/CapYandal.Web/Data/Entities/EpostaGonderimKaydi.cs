namespace CapYandal.Web.Data.Entities;

public class EpostaGonderimKaydi
{
    public Guid Id { get; set; }
    public Guid KullaniciId { get; set; }
    public string Saglayici { get; set; } = "Zimbra";
    public string Alici { get; set; } = string.Empty;
    public string Konu { get; set; } = string.Empty;
    public DateTimeOffset GonderimAni { get; set; }
    public EpostaGonderimDurumu Durum { get; set; }
    public string? HataOzeti { get; set; }
}
