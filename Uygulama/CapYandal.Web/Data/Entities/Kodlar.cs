namespace CapYandal.Web.Data.Entities;

public enum OgrenciProgramTuru
{
    Cap = 1,
    YanDal = 2
}

public enum AkademikYariyil
{
    Guz = 1,
    Bahar = 2
}

public enum DersTuru
{
    Teknik = 1,
    Sosyal = 2,
    Zorunlu = 3
}

public enum KosulDegerlendirmeTuru
{
    Otomatik = 1,
    Manuel = 2
}

public enum KosulIzinTuru
{
    DusukDonemSayisi = 1,
    ArdisikDusukDonem = 2
}

public enum DersSecimiDurumu
{
    OnayBekliyor = 1,
    Onaylandi = 2,
    Reddedildi = 3,
    IptalEdildi = 4
}

public enum BelgeKapsami
{
    Genel = 1,
    Kisisel = 2
}

public enum BelgeIcerikDurumu
{
    Mevcut = 1,
    Temizleniyor = 2,
    Kaldirildi = 3
}

public enum GanoKapsami
{
    AnaDal = 1,
    Cap = 2,
    YanDal = 3
}

public enum GanoSonucKodu
{
    Normal = 1,
    Istisna = 2,
    Ihlal = 3,
    BelirsizGecmisEksik = 4,
    DevreDisi = 5
}

public enum GanoSonucRengi
{
    Yesil = 1,
    Sari = 2,
    Kirmizi = 3
}

public enum EpostaGonderimDurumu
{
    Basarili = 1,
    Basarisiz = 2
}
