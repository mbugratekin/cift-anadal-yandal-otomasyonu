namespace CapYandal.Web.Models;

public sealed record GenelBelgeSatirModel(Guid Id, string Tur, string DosyaAdi, int SurumNo, string Surum, bool Indirilebilir,
    DateTimeOffset? GuncellemeAni = null)
{
    public Guid BelgeId { get; init; }
    public string BelgeSurumu { get; init; } = string.Empty;
    public bool Arsivde { get; init; }
    public IReadOnlyList<GenelBelgeSatirModel> Gecmis { get; init; } = [];
}
public sealed record GenelBelgeYuklemeModel(Guid? GenelBelgeId, string Tur, string? Surum, bool KategoriSec = false);
