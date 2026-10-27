using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CapYandal.Web.Models;

namespace CapYandal.Web.Services.Denetim;

// Denetim verisini değiştirmez; teknik adları öğretim elemanının okuyabileceği Türkçe özete dönüştürür.
public static partial class DenetimGorunumu
{
    private static readonly IReadOnlyDictionary<string, string> IslemAdlari = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OgrenciKaydi"] = "Öğrenci programa kaydedildi",
        ["DersListesiGonder"] = "Ders listesi gönderildi",
        ["DersListesiYenidenGonder"] = "Ders listesi yeniden gönderildi",
        ["DersListesiDuzelt"] = "Ders listesi düzeltildi",
        ["DersListesiOnayla"] = "Ders listesi onaylandı",
        ["DersListesiOnayiniGeriAl"] = "Ders listesi onayı geri alındı",
        ["MazeretliDersKaydiOnayla"] = "Mazeretli ders kaydı onaylandı",
        ["KaynakAnaDalKaydet"] = "Ana dal seçenekleri güncellendi",
        ["DersListesiReddet"] = "Ders listesi düzeltme için öğrenciye gönderildi",
        ["DersListesiIptal"] = "Ders listesi iptal edildi",
        ["DersBazindaDuzeltmeIste"] = "Seçili dersler için düzeltme istendi",
        ["GecmisDersIlkAktarim"] = "Geçmiş ders ve notlar aktarıldı",
        ["GecmisDonemSecenekleriniHazirla"] = "Geçmiş dönem seçenekleri hazırlandı",
        ["DersDenemesiKaydet"] = "Ders notu kaydedildi",
        ["DersDenemesiSurumle"] = "Ders notu düzeltildi",
        ["GanoEkle"] = "GANO kaydı eklendi",
        ["GanoDuzenle"] = "GANO kaydı güncellendi",
        ["GanoIlkGuncelDegerlendirme"] = "GANO akademik şartlara göre değerlendirildi",
        ["GanoSonucRevizyon"] = "GANO değerlendirmesi yeniden hesaplandı",
        ["SayisalKosulKontrolu"] = "Akademik şart sonucu kaydedildi",
        ["AkademikKosulSurumuEkle"] = "Akademik şartların yeni sürümü kaydedildi",
        ["SablonEkle"] = "Müfredat eklendi",
        ["SablonDuzenle"] = "Müfredat dersleri güncellendi",
        ["SablonDurumu"] = "Müfredat durumu değiştirildi",
        ["SablonAdDegistir"] = "Müfredat adı değiştirildi",
        ["OrtakMufredatEkle"] = "Ortak müfredat eklendi",
        ["OrtakMufredatDurumu"] = "Ortak müfredat durumu değiştirildi",
        ["KisiselEvrakGonder"] = "Öğrenci evrakı yüklendi",
        ["BelgeIstegineYanit"] = "Belge isteğine yanıt yüklendi",
        ["BelgeIstegiEkle"] = "Öğrenciden belge istendi",
        ["BelgeOnayla"] = "Öğrenci evrakı onaylandı",
        ["BelgeReddet"] = "Öğrenci evrakı reddedildi",
        ["BelgeIptal"] = "Öğrenci evrakı iptal edildi",
        ["BelgeKarari"] = "Belge inceleme kararı kaydedildi",
        ["OgrenciYenilemeKilidi"] = "Öğrenci belge düzenleme kilidi güncellendi",
        ["GenelBelgeSurumYuklendi"] = "Dokümanın yeni sürümü yüklendi",
        ["GenelBelgeArsivlendi"] = "Doküman arşivlendi",
        ["GenelBelgeArsivdenAlindi"] = "Doküman arşivden çıkarıldı",
        ["MesajGonder"] = "Mesaj gönderildi",
        ["MesajlarOkundu"] = "Mesajlar okundu",
        ["ProgramTakipNotuEkle"] = "Koordinatör notu eklendi",
        ["ProgramTakipNotuDuzenle"] = "Koordinatör notu güncellendi",
        ["ProgramTakipNotuIptal"] = "Koordinatör notu iptal edildi",
        ["DonemIzniEkle"] = "Kayıt dondurma kaydı eklendi",
        ["DonemIzniIptal"] = "Kayıt dondurma kaydı iptal edildi",
        ["ManuelProgramKarari"] = "Program kararı kaydedildi",
        ["IslemDonemiIlerlet"] = "İşlem dönemi değiştirildi",
        ["DersKaydiniAc"] = "Ders kaydı açıldı",
        ["DersKaydiniKapat"] = "Ders kaydı kapatıldı",
        ["ProfilFotografiOnayla"] = "Profil fotoğrafı onaylandı",
        ["Kaydet"] = "Görüşme bilgileri güncellendi",
        ["HesapHazirla"] = "Personel hesabı hazırlandı",
        ["IlkGorevAta"] = "Personel görevi atandı",
        ["PersonelGorevDevri"] = "Personel görevi devredildi",
        ["IlkDavetTalebi"] = "Öğrenci daveti istendi",
        ["PersonelDavetTalebi"] = "Personel daveti istendi",
        ["IlkParolaOlustur"] = "İlk parola oluşturuldu",
        ["PersonelParolaBelirle"] = "Personel parolası oluşturuldu",
        ["ParolaDegistir"] = "Parola değiştirildi",
        ["ParolaSifirla"] = "Parola sıfırlandı",
        ["KisiselEpostaTalep"] = "Kişisel e-posta doğrulaması istendi",
        ["KisiselEpostaDogrula"] = "Kişisel e-posta doğrulandı",
        ["OturumGiris"] = "Oturum açıldı",
        ["OturumCikis"] = "Oturum kapatıldı",
        ["GirisTalebi"] = "Giriş denemesi kaydedildi"
    };

    private static readonly IReadOnlyDictionary<string, string> AlanAdlari = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Kapsam"] = "Kapsam", ["Aciklama"] = "Açıklama", ["MazeretliMi"] = "Mazeretli kayıt",
        ["Gano"] = "GANO", ["Kaynak"] = "Kaynak", ["Gerekce"] = "Gerekçe", ["Durum"] = "Durum",
        ["KararGerekcesi"] = "Karar gerekçesi", ["AktifMi"] = "Program durumu", ["OgrenciKilidi"] = "Öğrenci düzenleme kilidi",
        ["Baslangic"] = "Başlangıç", ["Bitis"] = "Bitiş", ["Tur"] = "Tür", ["Metin"] = "Metin",
        ["Not"] = "Not", ["HarfNotu"] = "Harf notu", ["SayisalNot"] = "Sayısal not", ["MuafMi"] = "Muafiyet",
        ["Program"] = "Program", ["ProgramTuru"] = "Program", ["MufredatAdi"] = "Müfredat", ["Ad"] = "Ad",
        ["DosyaAdi"] = "Dosya", ["OnayDurumu"] = "Onay durumu", ["AcikMi"] = "Ders kaydı",
        ["AkademikYilBaslangici"] = "Akademik yıl", ["Yariyil"] = "Yarıyıl", ["KuralSayisi"] = "Şart sayısı",
        ["IletisimEpostasi"] = "İletişim e-postası", ["GorusmeKonumu"] = "Görüşme yeri", ["Araliklar"] = "Görüşme saatleri",
        ["OturumAcik"] = "Oturum açık", ["TalepAlindi"] = "Talep alındı",
        ["KisiselEposta"] = "Kişisel e-posta", ["BekleyenKisiselEposta"] = "Doğrulanmayı bekleyen kişisel e-posta",
        ["KisiselEpostaOnayliMi"] = "Kişisel e-posta doğrulandı", ["KisiselSurumNo"] = "Belge sürümü",
        ["KisiselTur"] = "Belge türü", ["YenilemeGerekcesi"] = "Yenileme gerekçesi"
    };

    private static readonly HashSet<string> TeknikAlanlar = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "ProgramId", "DonemId", "ListeId", "BelgeId", "GanoKaydiId", "KullaniciId", "OgrenciId",
        "GirenKullaniciId", "DuzeltenKullaniciId", "OnaylayanKullaniciId", "KararVerenKullaniciId", "OlusturanKullaniciId",
        "RowVersion", "Surum", "IstekId", "SeriId", "EkId", "GonderenKullaniciId", "DegisiklikAni", "OlusturulmaAni", "OnayAni", "KayitAni", "GonderimAni"
    };

    public static string IslemAdi(string value) => IslemAdlari.TryGetValue(value, out var label) ? label : KelimelereAyir().Replace(value, " $1").Trim();

    public static string VarlikAdi(string value) => value switch
    {
        "DersSecimi" => "Ders listesi", "DersDuzeltmeTalebi" => "Ders düzeltme isteği", "DersDenemesi" => "Ders notu",
        "GanoKontrolKaydi" or "GanoKontrolSonucu" => "GANO takibi", "OgrenciProgrami" => "Öğrenci programı",
        "ProgramDonemIzni" => "Kayıt dondurma", "ProgramTakipNotu" => "Koordinatör notu", "Belge" => "Belge",
        "EkBelgeIstegi" => "Belge isteği", "AkademikKosulSurumu" => "Akademik şartlar", "DersSablonu" or "Mufredat" => "Müfredat",
        "SohbetMesaji" => "Sohbet", "DersKayitKapisi" => "Ders kayıt dönemi", "Oturum" => "Oturum", "GuvenlikTalebi" => "Güvenlik talebi",
        "KisiselBelgeSerisi" => "Öğrenci belgesi", "UygulamaKullanicisi" => "Kullanıcı hesabı",
        _ => KelimelereAyir().Replace(value, " $1").Trim()
    };

    public static (IReadOnlyList<DenetimDegisiklikModeli> Degisiklikler, string? Gerekce) DegisiklikleriOku(string? onceki, string? sonraki, string? varlik = null)
    {
        using var oldDocument = BelgeyiAc(onceki);
        using var newDocument = BelgeyiAc(sonraki);
        var oldValues = AlanlariOku(oldDocument?.RootElement, varlik);
        var newValues = AlanlariOku(newDocument?.RootElement, varlik);
        JsonElement? reasonValue = newValues.TryGetValue("Gerekce", out var reasonElement) ? reasonElement
            : newValues.TryGetValue("KararGerekcesi", out var decisionReason) ? decisionReason : null;
        var reason = Deger(reasonValue);
        var keys = oldValues.Keys.Concat(newValues.Keys).Where(k => !TeknikAlanMi(k) && k is not ("Gerekce" or "KararGerekcesi"))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var changes = new List<DenetimDegisiklikModeli>();
        foreach (var key in keys)
        {
            var oldValue = oldValues.GetValueOrDefault(key); var newValue = newValues.GetValueOrDefault(key);
            if (oldValue.ValueKind == JsonValueKind.Object || newValue.ValueKind == JsonValueKind.Object) continue;
            var before = AnlamliDeger(key, oldValue, varlik); var after = AnlamliDeger(key, newValue, varlik);
            if (before == after || before == "—" && after == "—") continue;
            changes.Add(new(AlanAdlari.TryGetValue(key, out var name) ? name : KelimelereAyir().Replace(key, " $1").Trim(), before, after));
        }
        if (changes.Count == 0 && (!string.IsNullOrWhiteSpace(onceki) || !string.IsNullOrWhiteSpace(sonraki)))
        {
            var before = BasitDeger(onceki); var after = BasitDeger(sonraki);
            if (before != after) changes.Add(new("Değer", before, after));
        }
        return (changes, string.IsNullOrWhiteSpace(reason) || reason == "—" ? null : reason);
    }

    public static Guid? ProgramKimligi(string varlik, string anahtar, string? onceki, string? sonraki)
    {
        if (varlik == "OgrenciProgrami" && Guid.TryParse(anahtar, out var direct)) return direct;
        if (anahtar.Contains('/') && Guid.TryParse(anahtar.Split('/')[0], out var composite)) return composite;
        return JsonGuid(sonraki, "ProgramId") ?? JsonGuid(onceki, "ProgramId");
    }

    private static Dictionary<string, JsonElement> AlanlariOku(JsonElement? root, string? varlik)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (root is not { ValueKind: JsonValueKind.Object } value) return result;
        foreach (var property in value.EnumerateObject()) result[property.Name] = property.Value.Clone();
        // Eski not kaydı düz, yeni kayıt Deneme/Surum zarfındadır; iki taraf aynı alanlarla karşılaştırılır.
        if (varlik == "DersDenemesi")
            foreach (var key in new[] { "Deneme", "Surum" })
                if (value.TryGetProperty(key, out var nested) && nested.ValueKind == JsonValueKind.Object)
                    foreach (var property in nested.EnumerateObject()) result[property.Name] = property.Value.Clone();
        return result;
    }

    private static JsonDocument? BelgeyiAc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "null") return null;
        try { return JsonDocument.Parse(value); } catch (JsonException) { return null; }
    }

    private static Guid? JsonGuid(string? json, string name)
    {
        using var document = BelgeyiAc(json); if (document is null) return null;
        return GuidAra(document.RootElement, name);
    }

    private static Guid? GuidAra(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name) && property.Value.ValueKind == JsonValueKind.String && Guid.TryParse(property.Value.GetString(), out var id)) return id;
                var nested = GuidAra(property.Value, name); if (nested is not null) return nested;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) { var nested = GuidAra(item, name); if (nested is not null) return nested; }
        return null;
    }

    private static string AnlamliDeger(string key, JsonElement value, string? entity)
    {
        if (value.ValueKind == JsonValueKind.String && key is "DosyaAdi" or "Ad")
            return SunumGorunumu.BelgeAdi(Deger(value));
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) return Deger(value);
        if (key == "KisiselTur" || key == "Tur" && entity is "KisiselBelgeSerisi" or "EkBelgeIstegi")
            return number switch { 1 => "İmzalı form", 2 => "ÇAP/Yan Dal transkripti", 3 => "Ana dal transkripti", 4 => "Yönetim kurulu yazısı", 5 => "Diğer belge", _ => Deger(value) };
        if (entity == "DersDenemesi" && key == "Durum")
            return number switch { 1 => "Planlandı", 2 => "Alındı", 3 => "Geçti", 4 => "Kaldı", 5 => "Muaf", 6 => "Çıkarıldı", _ => Deger(value) };
        if (entity == "DersDenemesi" && key == "Kaynak")
            return number switch { 1 => "Öğrenci", 2 => "Koordinatör", 3 => "Geçmiş ders ve notlar", _ => Deger(value) };
        if (entity == "OgrenciProgrami" && key == "SonKarar")
            return number switch { 1 => "Mezuniyet", 2 => "Sertifika", 3 => "Kayıt kapatma", _ => Deger(value) };
        if (entity == "Donem" && key == "Yariyil")
            return number switch { 1 => "Güz", 2 => "Bahar", _ => Deger(value) };
        if (entity == "ProgramDonemIzni" && key is "Baslangic" or "Bitis" && number >= 4 && number <= 19997)
        {
            var year = number / 2;
            return $"{year}–{year + 1} {(number % 2 == 0 ? "Güz" : "Bahar")}";
        }
        if (key == "Kapsam" && entity is "GanoKontrolKaydi" or "GanoKontrolSonucu")
            return number switch { 1 => "Ana Dal", 2 => "ÇAP", 3 => "Yan Dal", _ => Deger(value) };
        if (key == "OnayDurumu" || key == "Durum" && entity is "DersSecimi" or "Belge")
            return number switch { 1 => "Onay bekliyor", 2 => "Onaylandı", 3 => entity == "DersSecimi" ? "Düzeltme istendi" : "Reddedildi", 4 => "İptal edildi", _ => Deger(value) };
        if (key is "Tur" or "ProgramTuru" && entity is "OgrenciProgrami" or "DersSablonu" or "KaynakAnaDal")
            return number switch { 1 => "ÇAP", 2 => "Yan Dal", _ => Deger(value) };
        return Deger(value);
    }

    private static string Deger(JsonElement? value)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "—";
        return value.Value.ValueKind switch
        {
            JsonValueKind.True => "Evet", JsonValueKind.False => "Hayır", JsonValueKind.String => TarihVeyaMetin(value.Value.GetString()),
            JsonValueKind.Array => $"{value.Value.GetArrayLength()} kayıt", JsonValueKind.Object => $"{value.Value.EnumerateObject().Count()} alan",
            JsonValueKind.Number when value.Value.TryGetDecimal(out var number) => number.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")),
            _ => value.Value.ToString()
        };
    }

    private static string TarihVeyaMetin(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
        ? TimeZoneInfo.ConvertTime(date, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
        : string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string BasitDeger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "null") return "—";
        var trimmed = value.Trim();
        return trimmed.Length > 80 || trimmed.StartsWith('{') || trimmed.StartsWith('[') ? "Teknik ayrıntı" : trimmed.Trim('"');
    }

    private static bool TeknikAlanMi(string alan) => TeknikAlanlar.Contains(alan)
        || alan.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
        || alan.EndsWith("Ani", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("(?<!^)([A-ZÇĞİÖŞÜ][a-zçğıöşü])", RegexOptions.CultureInvariant)]
    private static partial Regex KelimelereAyir();
}
