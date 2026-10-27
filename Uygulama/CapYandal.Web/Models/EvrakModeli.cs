namespace CapYandal.Web.Models;

public sealed record EvrakDosyaModel(Guid Id,string Ad,long? Boyut,string Durum,string Surum,bool Mevcut,bool Silinebilir)
{
    public Guid? SeriId { get; init; }
    public Guid? IstekId { get; init; }
    public int? SurumNo { get; init; }
    public Data.Entities.KisiselBelgeTuru? Tur { get; init; }
    public Data.Entities.BelgeOnayDurumu? Onay { get; init; }
    public string? Gerekce { get; init; }
    public bool GuncelMi { get; init; }
    public bool OgrenciKilidi { get; init; }
    public DateTimeOffset YuklemeAni { get; init; }
}
public sealed record EkBelgeIstekSatiri(Guid Id,Data.Entities.KisiselBelgeTuru Tur,string Metin,string Durum,DateTimeOffset Ani,string? Gerekce)
{
    public Data.Entities.EkBelgeIstegiDurumu DurumKodu { get; init; }
    public bool OgrenciKilidi { get; init; }
}
public sealed record EvrakBaglamModel(Guid ProgramId,Guid? DonemId,Guid? ListeId,string Ogrenci,string Donem,bool Yuklenebilir,IReadOnlyList<EvrakDosyaModel> Dosyalar)
{
    public string? OgrenciNo { get; init; }
    public bool Inceleyebilir { get; init; }
    public IReadOnlyList<EkBelgeIstekSatiri> Istekler { get; init; } = [];
    public bool GecmisDonemMi { get; init; }
    public IReadOnlyList<EvrakBaglamModel> GecmisDonemler { get; init; } = [];
}
public sealed record EvrakSayfaModel(int Yil,int Yariyil,bool IslemDonemiBelirli,IReadOnlyList<EvrakBaglamModel> Baglamlar);
