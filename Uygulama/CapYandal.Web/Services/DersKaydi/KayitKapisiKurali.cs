namespace CapYandal.Web.Services.DersKaydi;

// Yalnız tarih aralığının ders kaydına açık olup olmadığını hesaplar.
public sealed record KapiDurumu(Guid DonemId, bool AcikMi,
    DateTimeOffset? AcilisAniUtc = null, DateTimeOffset? KapanisAniUtc = null);

public static class KayitKapisiKurali
{
    public static KapiDurumu EsikleriUygula(KapiDurumu durum, DateTimeOffset sunucuAni)
    {
        var next = durum;
        if (next.AcilisAniUtc is { } start && sunucuAni >= start)
            next = next with { AcikMi = true, AcilisAniUtc = null };
        if (next.KapanisAniUtc is { } end && sunucuAni >= end)
            next = next with { AcikMi = false, AcilisAniUtc = null, KapanisAniUtc = null };
        return next;
    }

    public static bool Baslat(KapiDurumu durum, Guid donemId, DateTimeOffset sunucuAni,
        DateTimeOffset? baslangic, DateTimeOffset? bitis, out KapiDurumu next)
    {
        next = durum;
        if (donemId == Guid.Empty || durum.DonemId != donemId || EsikleriUygula(durum, sunucuAni).AcikMi)
            return false;
        if (baslangic is { } start && start <= sunucuAni) return false;
        if (bitis is { } end && end <= (baslangic ?? sunucuAni)) return false;
        next = new(donemId, baslangic is null, baslangic?.ToUniversalTime(), bitis?.ToUniversalTime());
        return true;
    }

    public static bool Kapat(KapiDurumu durum, Guid donemId, DateTimeOffset sunucuAni,
        DateTimeOffset? bitis, out KapiDurumu next)
    {
        next = durum;
        if (donemId == Guid.Empty || durum.DonemId != donemId) return false;
        if (bitis is { } end && (end <= sunucuAni || !EsikleriUygula(durum, sunucuAni).AcikMi))
            return false;
        next = new(donemId, bitis is not null, null, bitis?.ToUniversalTime());
        return true;
    }

    // Bu bool'lar yetkili sunucu okumasından gelir; HTTP formundan kabul edilmez.
    public static bool OgrenciSecimYapabilir(KapiDurumu durum, Guid donemId,
        bool programAktif, bool gonderilmisListeVar, DateTimeOffset sunucuAni) =>
        donemId != Guid.Empty && durum.DonemId == donemId && programAktif &&
        !gonderilmisListeVar && EsikleriUygula(durum, sunucuAni).AcikMi;
}
