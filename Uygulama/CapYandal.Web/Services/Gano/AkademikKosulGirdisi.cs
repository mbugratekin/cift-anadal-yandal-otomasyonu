using CapYandal.Web.Data.Entities;
using System.Text.Json.Serialization;

namespace CapYandal.Web.Services.Gano;

public sealed record AkademikKuralGirdisi(Guid? KuralKimligi, GanoKapsami Kapsam, GanoKontrolAmaci Amac,
    GanoKontrolTuru KontrolTuru, [property: JsonRequired] decimal NormalEsik, decimal? AltEsik, int? DonemSayisi, string? Aciklama);
public sealed record AkademikKosulGirdisi(OgrenciProgramTuru ProgramTuru, Guid? BeklenenSurumId,
    IReadOnlyList<AkademikKuralGirdisi>? Kurallar)
{
    public IReadOnlyList<SayisalKuralGirdisi>? SayisalKurallar { get; init; }
}
public enum AkademikKosulDurumu { Basarili, Gecersiz, IzinYok, Cakisma, GecmisUyarlamasiGerekli, Basarisiz }
public sealed record AkademikKosulKaydetSonucu(AkademikKosulDurumu Durum, Guid? SurumId = null, string? Hata = null);
public sealed record AkademikKosulOkuma(AkademikKosulDurumu Durum, Guid? SurumId = null, int? SurumNo = null,
    DateTimeOffset? KayitAni = null, IReadOnlyList<AkademikKuralGirdisi>? Kurallar = null)
{
    public IReadOnlyList<SayisalKuralGirdisi> SayisalKurallar { get; init; } = [];
}

public static class AkademikKosulGirdiKurali
{
    public static string? Hata(OgrenciProgramTuru program, IReadOnlyList<AkademikKuralGirdisi>? rules)
    {
        if (program is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal)) return "Program türünü seçin.";
        if (rules is null || rules.Count > 100) return "Geçerli kural listesi gönderin.";
        var ids = new HashSet<Guid>(); var definitions = new HashSet<(GanoKapsami, GanoKontrolAmaci)>();
        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r is null) return "Geçerli kural gönderin.";
            var scope = program == OgrenciProgramTuru.Cap ? GanoKapsami.Cap : GanoKapsami.YanDal;
            if (r.Kapsam != GanoKapsami.AnaDal && r.Kapsam != scope) return "Kuralın GANO kapsamı programla uyuşmuyor.";
            if (r.Amac is not (GanoKontrolAmaci.Devam or GanoKontrolAmaci.Tamamlama) ||
                !definitions.Add((r.Kapsam, r.Amac))) return "Aynı GANO kapsamı ve amaç için tek kural kullanın.";
            if (r.KuralKimligi is Guid id && (id == Guid.Empty || !ids.Add(id))) return "Geçerli, tekrarlanmayan kural kimliği gerekir.";
            if (!Esik(r.NormalEsik)) return "GANO 0–4 arasında ve en fazla iki ondalık haneli olmalı.";
            switch (r.KontrolTuru)
            {
                case GanoKontrolTuru.TekEsik:
                    if (r.AltEsik is not null || r.DonemSayisi is not null) return "Bu koşulda alt sınır ve dönem sayısı uygulanmaz.";
                    break;
                case GanoKontrolTuru.DusmeHakki:
                    if (r.AltEsik is decimal lower && (!Esik(lower) || lower > r.NormalEsik) || r.DonemSayisi is null or < 0)
                        return "Varsa alt sınırı ve negatif olmayan tam dönem sayısını kontrol edin.";
                    break;
                case GanoKontrolTuru.UstUsteDusukDonem:
                    if (r.AltEsik is not null || r.DonemSayisi is null or < 1) return "Alt sınır uygulanmaz; en az bir dönem gerekir.";
                    break;
                default: return "Kontrol türünü seçin.";
            }
            if (string.IsNullOrWhiteSpace(r.Aciklama) || r.Aciklama.Length > 2000 ||
                r.Aciklama.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
                return $"{i + 1}. kural açıklaması boş olamaz ve 2000 karakteri aşamaz.";
        }
        return null;
    }
    private static bool Esik(decimal value) => value is >= 0 and <= 4 && decimal.Round(value, 2) == value;
}
