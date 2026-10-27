using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Models;

public sealed record MufredatSecenekModel(Guid Id, string Ad, OgrenciProgramTuru Tur, string? KaynakAnaDal)
{
    public Guid? OrtakMufredatId { get; init; }
    public int? BaslangicYili { get; init; }
    public int? SonrakiBaslangicYili { get; init; }
}
