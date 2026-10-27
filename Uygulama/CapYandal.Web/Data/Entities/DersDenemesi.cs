namespace CapYandal.Web.Data.Entities;

public enum KayitOnayDurumu { OnayBekliyor = 1, Onaylandi = 2, Reddedildi = 3, IptalEdildi = 4 }
public enum DersDenemeDurumu { Planlandi = 1, Alindi = 2, Gecti = 3, Kaldi = 4, Muaf = 5, Cikarildi = 6 }
public enum DersKayitKaynagi { Ogrenci = 1, Koordinator = 2, GecmisAktarim = 3 }

// Ders denemesi — kimlik ve gönderilmiş katalog kopyası sabittir; sonuçlar ayrı sürümlerde tutulur.
public sealed class DersDenemesi
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid DonemId { get; set; }
    public Guid DersSecimKalemiId { get; set; }
    public DersKayitKaynagi Kaynak { get; set; }
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturmaAni { get; set; }
}

// Sonuç sürümü — onay, ret ve düzeltme yeni satırdır; eski not ve karar değiştirilmez.
public sealed class DersDenemeSurumu
{
    public Guid Id { get; set; }
    public Guid DenemeId { get; set; }
    public int SurumNo { get; set; }
    public DersDenemeDurumu Durum { get; set; }
    public string? Not { get; set; }
    public decimal? SayisalNot { get; set; }
    public Guid? GecilenDonemId { get; set; }
    public KayitOnayDurumu OnayDurumu { get; set; } = KayitOnayDurumu.OnayBekliyor;
    public string? Gerekce { get; set; }
    public Guid YapanKullaniciId { get; set; }
    public DateTimeOffset IslemAni { get; set; }
}
