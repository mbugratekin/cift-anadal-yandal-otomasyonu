namespace CapYandal.Web.Data.Entities;
public sealed class SohbetTemizlikDurumu
{
    public int Id { get; set; } = 1;
    public int SonIslenenDonem { get; set; }
    public DateTimeOffset? SonCalismaAni { get; set; }
}
