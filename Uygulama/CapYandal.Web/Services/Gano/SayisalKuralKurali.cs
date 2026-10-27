using System.Text.Json;
using CapYandal.Web.Models;

namespace CapYandal.Web.Services.Gano;

public enum SayisalOlcut { TamamlananDers = 1, TamamlananKredi = 2, TamamlananAkts = 3, KabuldenSonraGecenDonem = 4, BasvuruYariyili = 5, ArdisikKayitsizDonem = 6, HavuzAkts = 7, SecilenDers = 8, SecilenAkts = 9 }
public sealed record SayisalEkVeri(decimal? HavuzAkts, decimal? SecilenDers, decimal? SecilenAkts);
public sealed record SayisalKuralGirdisi(SayisalOlcut Olcut, Guid? MufredatId, decimal? AltSinir, decimal? UstSinir, string Dayanak);
public sealed record SayisalKuralSonucu(SayisalOlcut Olcut, string Ad, decimal? Deger, decimal? AltSinir, decimal? UstSinir, string Durum, string Aciklama, string Dayanak);

// Sayısal koşullar — değerleri koordinatör belirler; eksik veri veya kapsamdan akademik hüküm üretilmez.
public static class SayisalKuralKurali
{
    public static string Ad(SayisalOlcut olcut) => olcut switch
    {
        SayisalOlcut.TamamlananDers => "Onaylı tamamlanan ders sayısı",
        SayisalOlcut.TamamlananKredi => "Onaylı tamamlanan kredi",
        SayisalOlcut.TamamlananAkts => "Onaylı tamamlanan AKTS",
        SayisalOlcut.KabuldenSonraGecenDonem => "Kabulden sonra geçen takvim dönemi (pasif dönemler dâhil)",
        SayisalOlcut.BasvuruYariyili => "Başvuru yarıyılı aralığı",
        SayisalOlcut.ArdisikKayitsizDonem => "Ardışık onaylı ders kaydı görünmeyen dönem",
        SayisalOlcut.HavuzAkts => "Atanmış ders havuzunun AKTS toplamı",
        SayisalOlcut.SecilenDers => "Onaylı kişisel listedeki ders sayısı",
        SayisalOlcut.SecilenAkts => "Onaylı kişisel listenin AKTS toplamı",
        _ => "Bilinmeyen ölçüt"
    };
    public static string? Hata(IReadOnlyList<SayisalKuralGirdisi>? kurallar)
    {
        if (kurallar is null || kurallar.Count > 30) return "En fazla 30 sayısal kural girin.";
        foreach (var kural in kurallar)
        {
            if (kural is null || !Enum.IsDefined(kural.Olcut) || kural.MufredatId == Guid.Empty ||
                kural.AltSinir is null && kural.UstSinir is null ||
                kural.AltSinir is < 0 or > 1000000 || kural.UstSinir is < 0 or > 1000000 || kural.AltSinir > kural.UstSinir)
                return "Ölçüt, kapsam ve alt/üst sınırları kontrol edin.";
            var tamSayi = kural.Olcut is SayisalOlcut.TamamlananDers or SayisalOlcut.SecilenDers or SayisalOlcut.KabuldenSonraGecenDonem or SayisalOlcut.BasvuruYariyili or SayisalOlcut.ArdisikKayitsizDonem;
            foreach (var deger in new[] { kural.AltSinir, kural.UstSinir }.OfType<decimal>())
                if (decimal.Round(deger, tamSayi ? 0 : 2) != deger) return "Ders/dönem tam sayı, kredi/AKTS en fazla iki ondalık olmalı.";
            if (string.IsNullOrWhiteSpace(kural.Dayanak) || kural.Dayanak.Length > 1000 ||
                kural.Dayanak.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
                return "Her sayısal kural için en fazla 1000 karakterlik resmî dayanak/açıklama girin.";
        }
        for (var i = 0; i < kurallar.Count; i++)
            for (var j = i + 1; j < kurallar.Count; j++)
                if (kurallar[i].Olcut == kurallar[j].Olcut && (kurallar[i].MufredatId is null || kurallar[j].MufredatId is null || kurallar[i].MufredatId == kurallar[j].MufredatId))
                    return "Aynı ölçütte çakışan program/müfredat kuralları eklenemez.";
        return JsonSerializer.Serialize(kurallar).Length > 16000 ? "Sayısal kural metinlerinin toplamı çok uzun." : null;
    }
    public static IReadOnlyList<SayisalKuralGirdisi>? Oku(string json)
    {
        try { var rules = JsonSerializer.Deserialize<SayisalKuralGirdisi[]>(json); return Hata(rules) is null ? rules : null; }
        catch (JsonException) { return null; }
    }
    public static SayisalKuralSonucu Degerlendir(SayisalKuralGirdisi kural, DersIlerlemeModeli ilerleme, bool onayliListeVar, int? gecenDonem, bool kapsamBelirli = true, int? kayitsizDonem = null, SayisalEkVeri? ek = null)
    {
        var dersOlcutu = kural.Olcut is SayisalOlcut.TamamlananDers or SayisalOlcut.TamamlananKredi or SayisalOlcut.TamamlananAkts;
        decimal? deger = kural.Olcut switch
        {
            SayisalOlcut.TamamlananDers when onayliListeVar => ilerleme.TamamlananSayisi,
            SayisalOlcut.TamamlananKredi when onayliListeVar => ilerleme.TamamlananKredi,
            SayisalOlcut.TamamlananAkts when onayliListeVar => ilerleme.TamamlananAkts,
            SayisalOlcut.KabuldenSonraGecenDonem => gecenDonem,
            SayisalOlcut.ArdisikKayitsizDonem => kayitsizDonem,
            SayisalOlcut.HavuzAkts when kapsamBelirli => ek?.HavuzAkts,
            SayisalOlcut.SecilenDers when kapsamBelirli && onayliListeVar => ek?.SecilenDers,
            SayisalOlcut.SecilenAkts when kapsamBelirli && onayliListeVar => ek?.SecilenAkts,
            _ => null
        };
        if (dersOlcutu && (!kapsamBelirli || ilerleme.Dersler.Count == 0 || ilerleme.Dersler.Any(x => x.KopyaCeliskisi))) deger = null;
        var durum = deger is null ? "Belirsiz" : deger < kural.AltSinir || deger > kural.UstSinir ? "Uyari" : "Aralikta";
        var aciklama = dersOlcutu && !kapsamBelirli ? "Müfredat bağı veya ders kopyaları uyuşmuyor; eşdeğerlik varsayılmadı." : deger is null ? kural.Olcut == SayisalOlcut.BasvuruYariyili
            ? "Başvuru kaydı bu uygulamada tutulmadığından değerlendirilmedi."
            : "Gerekli onaylı kayıt, kesin kredi/AKTS veya kabul dönemi bilgisi yok."
            : kural.Olcut == SayisalOlcut.KabuldenSonraGecenDonem
            ? "Kabul dönemi sıfır kabul edilerek takvimde geçen dönemler sayıldı; pasif dönemler çıkarılmadı."
            : "Yalnız kayıtlı onaylı kişisel ders listeleri ve onaylı geçti/muaf sonuçlarıdır; tüm müfredatın tamamlandığı anlamına gelmez.";
        if (kural.Olcut == SayisalOlcut.ArdisikKayitsizDonem)
            aciklama = "Yalnız işlem döneminden önce biten dönemler incelenir; izin/dondurma ardışıklığı keser. Eksik kayıt ders alınmadığını kanıtlamaz; koordinatör resmî kayıtlarla doğrular. Bekleyen inceleme varsa sonuç belirsizdir.";
        if (kural.Olcut == SayisalOlcut.HavuzAkts) aciklama = "Öğrenciye atanmış havuzdaki etkin derslerin toplamıdır; kişisel seçim ve başarı toplamı değildir. Eksik havuz/AKTS bilgisinde belirsizdir.";
        if (kural.Olcut is SayisalOlcut.SecilenDers or SayisalOlcut.SecilenAkts) aciklama = "İlgili döneme kadar onaylı kişisel listelerdeki çıkarılmamış dersler tek sayılır; seçilmiş olmak başarılı olmak değildir. Henüz seçilmeyen zorunlulukların tamamlandığını göstermez.";
        return new(kural.Olcut, Ad(kural.Olcut), deger, kural.AltSinir, kural.UstSinir, durum, aciklama, kural.Dayanak);
    }
}
