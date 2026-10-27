using System.Globalization;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Services.Gano;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Ogrenci;

// Riskli manuel kararı ilk tıklamada durdurur; ikinci tıklama mevcut yetki ve sürüm kontrollerinden yine geçer.
public sealed class ProgramKararUyariServisi(AppDbContext db, SayisalKosulServisi sayisal, GanoEkranServisi gano)
{
    public async Task<string?> OkuAsync(Guid actorId, Guid programId, ProgramKararTuru karar, CancellationToken ct)
    {
        var programType = await db.OgrenciProgramlari.AsNoTracking().Where(x => x.Id == programId && x.HedefBolum == "Bilgisayar Mühendisliği")
            .Select(x => (OgrenciProgramTuru?)x.Tur).SingleOrDefaultAsync(ct);
        if (programType is null || !ProgramKararKurali.UygunMu(programType.Value, karar)) return null;
        var currentPeriod = await db.Donemler.AsNoTracking().Where(x => x.IslemDonemiMi)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var ganoRows = await gano.OkuAsync(actorId, programId, ct);
        if (ganoRows is null) return null; // Yetkisiz/bulunamayan kaydı asıl servis sonuçlandırır.

        var latest = ganoRows.GroupBy(x => (x.Yil, x.Yariyil)).OrderBy(x => x.Key.Yil)
            .ThenBy(x => x.Key.Yariyil == "Güz" ? 0 : 1).LastOrDefault()?.ToArray() ?? [];
        var red = latest.Any(x => x.Renk == GanoSonucRengi.Kirmizi);

        if (karar == ProgramKararTuru.KayitKapatma)
            return red ? null : "Güncel GANO değerlendirmesinde ilişik kesme gerektiren kırmızı sonuç yok. Kaydı yine de kapatacaksanız Kararı tekrar kaydet düğmesine basın.";

        var problems = new List<string>();
        if (latest.Length == 0) problems.Add("GANO kaydı bulunmuyor");
        else if (red) problems.Add("güncel GANO değerlendirmesi kırmızı");
        else if (latest.Any(x => x.Renk is null)) problems.Add("GANO değerlendirmesi tamamlanmamış");

        if (currentPeriod is null) problems.Add("işlem dönemi bulunmuyor");
        else
        {
            var numeric = await sayisal.OkuAsync(actorId, programId, currentPeriod.Value, ct);
            if (numeric.Durum != 200 || numeric.Kontrol is null) problems.Add("akademik şartlar kesin değerlendirilemedi");
            else
                problems.AddRange(numeric.Kontrol.Sonuclar.Where(x => x.Durum != "Aralikta").Select(x =>
                    $"{x.Ad}: {(x.Deger?.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) ?? "belirsiz")}"));
        }
        if (problems.Count == 0) return null;
        return $"Bu karar için kontrol edilmesi gereken durumlar var: {string.Join("; ", problems)}. Devam edecekseniz Kararı tekrar kaydet düğmesine basın.";
    }
}
