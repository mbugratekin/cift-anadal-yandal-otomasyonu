namespace CapYandal.Web.Models;

public sealed record DersIlerlemeSatiri(Guid DersId, string Kod, string Ad, decimal? Kredi, decimal? Akts,
    bool Tamamlandi, bool KopyaCeliskisi, int DenemeSayisi);

public sealed record DersIlerlemeModeli(IReadOnlyList<DersIlerlemeSatiri> Dersler, int BekleyenListeSayisi)
{
    public int TamamlananSayisi => Dersler.Count(x => x.Tamamlandi);
    public int KalanSayisi => Dersler.Count - TamamlananSayisi;
    public decimal? TamamlananKredi => Toplam(true, false);
    public decimal? TamamlananAkts => Toplam(true, true);
    public decimal? KalanKredi => Toplam(false, false);
    public decimal? KalanAkts => Toplam(false, true);

    private decimal? Toplam(bool completed, bool ects)
    {
        var values = Dersler.Where(x => x.Tamamlandi == completed).Select(x => ects ? x.Akts : x.Kredi).ToArray();
        return values.Any(x => x is null) ? null : values.Sum(x => x!.Value);
    }
}
