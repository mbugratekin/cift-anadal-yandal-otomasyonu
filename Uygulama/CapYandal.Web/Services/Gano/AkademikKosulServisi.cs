using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CapYandal.Web.Services.Gano;

public sealed class AkademikKosulServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<AkademikKosulServisi> logger)
{
    private async Task<bool> YetkiliAsync(Guid actorId, bool write, CancellationToken ct)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null) return false;
        await db.Entry(actor).ReloadAsync(ct);
        var p = await access.OkuAsync(actor);
        return p.Izinli && !p.ParolaDegisikligiGerekli &&
            (p.Roller.Contains(UygulamaRolleri.Koordinator) || !write && p.Roller.Contains(UygulamaRolleri.BolumBaskani));
    }
    // ŞARTLARI OKUMA: Programın son tamamlanmış sürümü ve ilgili kurallar yüklenir.
    public async Task<AkademikKosulOkuma> OkuAsync(Guid actorId, OgrenciProgramTuru program, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || program is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal)) return new(AkademikKosulDurumu.Gecersiz);
        if (!await YetkiliAsync(actorId, false, ct)) return new(AkademikKosulDurumu.IzinYok);
        var header = await db.AkademikKosulSurumleri.AsNoTracking().Where(v => v.ProgramTuru == program)
            .OrderByDescending(v => v.SurumNo).FirstOrDefaultAsync(ct);
        if (header is null)
            return await db.KosulKumeleri.AnyAsync(k => k.ProgramTuru == program && k.AkademikKosulSurumuId == null, ct)
                ? new(AkademikKosulDurumu.GecmisUyarlamasiGerekli)
                : new(AkademikKosulDurumu.Basarili, Kurallar: Array.Empty<AkademikKuralGirdisi>());
        if (!header.TamamlandiMi) return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
        var rows = await (from r in db.KosulDegerleri.AsNoTracking() join k in db.KosulKumeleri.AsNoTracking() on r.KosulKumesiId equals k.Id
            where k.AkademikKosulSurumuId == header.Id orderby r.SiraNo
            select new { Rule = r, k.Kapsam }).ToListAsync(ct);
        if (rows.Any(x => x.Rule.KuralKimligi is null || x.Rule.Amac is null || x.Rule.KontrolTuru is null ||
            x.Rule.NormalEsik is null || x.Rule.SiraNo is null || x.Rule.Aciklama is null))
            return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
        var rules = rows.Select(x => new AkademikKuralGirdisi(x.Rule.KuralKimligi, x.Kapsam, x.Rule.Amac!.Value,
            x.Rule.KontrolTuru!.Value, x.Rule.NormalEsik!.Value, x.Rule.AltEsik,
            x.Rule.KontrolTuru == GanoKontrolTuru.DusmeHakki ? x.Rule.IzinSayisi : x.Rule.ArdisikDonemSayisi, x.Rule.Aciklama)).ToArray();
        if (AkademikKosulGirdiKurali.Hata(program, rules) is not null) return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
        var sayisal = SayisalKuralKurali.Oku(header.SayisalKurallarJson);
        if (sayisal is null) return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
        return new(AkademikKosulDurumu.Basarili, header.Id, header.SurumNo, header.OlusturulmaAni, Array.AsReadOnly(rules)) { SayisalKurallar = sayisal };
    }

    // ŞART KAYDI: Güncel sürüm denetlenir; eski kurallar değiştirilmez, yeni ve tamamlanmış bir sürüm eklenir.
    public async Task<AkademikKosulKaydetSonucu> KaydetAsync(Guid actorId, AkademikKosulGirdisi input, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || input is null || input.BeklenenSurumId == Guid.Empty) return new(AkademikKosulDurumu.Gecersiz);
        // Kaydetme başlamadan önce form değerlerinin kopyası alınır.
        var rules = input.Kurallar?.ToArray();
        var requestedNumeric = input.SayisalKurallar?.ToArray();
        if (requestedNumeric is not null && SayisalKuralKurali.Hata(requestedNumeric) is { } numericError)
            return new(AkademikKosulDurumu.Gecersiz, Hata: numericError);
        var error = AkademikKosulGirdiKurali.Hata(input.ProgramTuru, rules);
        if (error is not null || rules is null) return new(AkademikKosulDurumu.Gecersiz, Hata: error ?? "Geçerli kural listesi gönderin.");
        if (db.ChangeTracker.HasChanges()) return new(AkademikKosulDurumu.Basarisiz);
        var written = new List<object>(); bool completed = false;
        try
        {
            if (db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return new(AkademikKosulDurumu.Basarisiz);
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                if (!await YetkiliAsync(actorId, true, ct)) return new(AkademikKosulDurumu.IzinYok);
                // Aynı program ve sürümün tekrar yazılması engellenir.
                var latest = await db.AkademikKosulSurumleri.FromSqlInterpolated($"SELECT * FROM [AkademikKosulSurumleri] WITH (UPDLOCK,HOLDLOCK,INDEX(IX_AkademikKosulSurumleri_ProgramTuru_SurumNo)) WHERE [ProgramTuru] = {(int)input.ProgramTuru}")
                    .AsNoTracking().OrderByDescending(v => v.SurumNo).FirstOrDefaultAsync(ct);
                if (latest?.Id != input.BeklenenSurumId) return new(AkademikKosulDurumu.Cakisma);
                if (latest is { TamamlandiMi: false } || latest?.SurumNo == int.MaxValue || latest is null &&
                    await db.KosulKumeleri.AnyAsync(k => k.ProgramTuru == input.ProgramTuru && k.AkademikKosulSurumuId == null, ct))
                    return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
                var requestedIds = rules!.Where(r => r.KuralKimligi.HasValue).Select(r => r.KuralKimligi!.Value).ToArray();
                // Başka sürüme ait koşul kimliği yeniden kullanılamaz.
                var stored = await (from r in db.KosulDegerleri.AsNoTracking() join k in db.KosulKumeleri.AsNoTracking() on r.KosulKumesiId equals k.Id
                    where r.KuralKimligi != null && requestedIds.Contains(r.KuralKimligi.Value)
                    select new { r.KuralKimligi, k.ProgramTuru, k.AkademikKosulSurumuId }).ToListAsync(ct);
                if (stored.Any(s => s.ProgramTuru != input.ProgramTuru) || requestedIds.Any(id =>
                    stored.Any(s => s.KuralKimligi == id) && !stored.Any(s => s.KuralKimligi == id && s.AkademikKosulSurumuId == latest?.Id)))
                    return new(AkademikKosulDurumu.Gecersiz, Hata: "Kural kimliği güncel listeyle uyuşmuyor.");
                var now = clock.GetUtcNow().ToUniversalTime();
                var previousNumeric = latest is null ? [] : SayisalKuralKurali.Oku(latest.SayisalKurallarJson);
                if (previousNumeric is null) return new(AkademikKosulDurumu.GecmisUyarlamasiGerekli);
                var numericRules = requestedNumeric ?? previousNumeric.ToArray();
                var templateIds = numericRules.Where(x => x.MufredatId.HasValue).Select(x => x.MufredatId!.Value).Distinct().ToArray();
                if (await db.DersSablonlari.CountAsync(x => templateIds.Contains(x.Id) && x.Tur == input.ProgramTuru && x.HedefBolum == "Bilgisayar Mühendisliği", ct) != templateIds.Length)
                    return new(AkademikKosulDurumu.Gecersiz, Hata: "Sayısal kural müfredatı seçili programla uyuşmuyor.");
                if (latest is not null)
                {
                    var current = await OkuAsync(actorId, input.ProgramTuru, ct);
                    if (current.Durum != AkademikKosulDurumu.Basarili) return new(current.Durum);
                    var unchanged = (current.Kurallar ?? []).Select(r => r with { Aciklama = r.Aciklama?.Trim() })
                        .SequenceEqual(rules.Select(r => r with { Aciklama = r.Aciklama?.Trim() })) &&
                        previousNumeric.Select(r => r with { Dayanak = r.Dayanak.Trim() })
                        .SequenceEqual(numericRules.Select(r => r with { Dayanak = r.Dayanak.Trim() }));
                    if (unchanged)
                    {
                        if (own is not null) await tx.CommitAsync(ct);
                        completed = true;
                        return new(AkademikKosulDurumu.Basarili, latest.Id);
                    }
                }
                var previousRules = await KuralKopyasiAsync(latest?.Id, ct);
                var version = new AkademikKosulSurumu { Id = Guid.NewGuid(), ProgramTuru = input.ProgramTuru,
                    SurumNo = (latest?.SurumNo ?? 0) + 1, OlusturanKullaniciId = actorId, OlusturulmaAni = now,
                    SayisalKurallarJson = JsonSerializer.Serialize(numericRules.Select(x => x with { Dayanak = x.Dayanak.Trim() }).ToArray()) };
                db.AkademikKosulSurumleri.Add(version); written.Add(version);
                // Koşul sürümü yalnız bu işlem içinde güncellenir.
                await db.SaveChangesAsync(ct);
                var groups = new Dictionary<GanoKapsami, Guid>();
                foreach (var r in rules)
                {
                    if (!groups.TryGetValue(r.Kapsam, out var setId))
                    {
                        setId = Guid.NewGuid(); groups.Add(r.Kapsam, setId);
                        var set = new KosulKumesi { Id = setId, AkademikKosulSurumuId = version.Id, ProgramTuru = input.ProgramTuru,
                            Kapsam = r.Kapsam, OlusturanKullaniciId = actorId, OlusturulmaAni = now,
                            // Bu tarih sistem uyumluluğu için saklanır.
                            YururlukBaslangici = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(3)).DateTime) };
                        db.KosulKumeleri.Add(set); written.Add(set);
                    }
                    var identity = r.KuralKimligi ?? Guid.NewGuid();
                    var rule = new KosulDegeri { Id = Guid.NewGuid(), KosulKumesiId = setId, KuralKimligi = identity, KuralKodu = identity.ToString("N"),
                        Amac = r.Amac, KontrolTuru = r.KontrolTuru, SiraNo = Array.IndexOf(rules, r) + 1, Aciklama = r.Aciklama!.Trim(),
                        NormalEsik = r.NormalEsik, AltEsik = r.AltEsik,
                        IzinSayisi = r.KontrolTuru == GanoKontrolTuru.DusmeHakki ? r.DonemSayisi : null,
                        ArdisikDonemSayisi = r.KontrolTuru == GanoKontrolTuru.UstUsteDusukDonem ? r.DonemSayisi : null,
                        IzinTuru = r.KontrolTuru == GanoKontrolTuru.DusmeHakki ? KosulIzinTuru.DusukDonemSayisi :
                            r.KontrolTuru == GanoKontrolTuru.UstUsteDusukDonem ? KosulIzinTuru.ArdisikDusukDonem : null,
                        DegerlendirmeTuru = KosulDegerlendirmeTuru.Otomatik };
                    db.KosulDegerleri.Add(rule); written.Add(rule);
                }
                await db.SaveChangesAsync(ct);
                version.TamamlandiMi = true;
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(AkademikKosulSurumu), VarlikAnahtari = version.Id.ToString(),
                    Islem = "AkademikKosulSurumuEkle", OncekiDeger = JsonSerializer.Serialize(new { OncekiSurumId = latest?.Id, Kurallar = previousRules, SayisalKurallar = previousNumeric }),
                    YeniDeger = JsonSerializer.Serialize(new { version.ProgramTuru, version.SurumNo, version.OlusturulmaAni, KuralSayisi = rules.Length,
                        Kurallar = await KuralKopyasiAsync(version.Id, ct), SayisalKurallar = numericRules }),
                    YapanKullaniciId = actorId, IslemAni = now };
                db.DegisiklikGecmisi.Add(audit); written.Add(audit);
                await db.SaveChangesAsync(ct);
                if (own is not null) await own.CommitAsync(ct);
                completed = true; return new(AkademikKosulDurumu.Basarili, version.Id);
            }
            finally
            {
                if (!completed)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await own.RollbackAsync(CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return new(AkademikKosulDurumu.Cakisma); }
        catch (Exception ex) { logger.LogWarning("Koşul sürümü kaydedilemedi. Tür: {Tur}", ex.GetType().Name); return new(AkademikKosulDurumu.Basarisiz); }
        finally { if (!completed) foreach (var row in written) db.Entry(row).State = EntityState.Detached; }
    }

    // İşlem geçmişine yalnız izin verilen akademik alanlar kopyalanır.
    private async Task<object> KuralKopyasiAsync(Guid? versionId, CancellationToken ct) =>
        await (from rule in db.KosulDegerleri.AsNoTracking()
            join set in db.KosulKumeleri.AsNoTracking() on rule.KosulKumesiId equals set.Id
            where versionId != null && set.AkademikKosulSurumuId == versionId
            orderby rule.SiraNo, rule.KuralKodu
            select new { rule.KuralKimligi, rule.KuralKodu, set.Kapsam, rule.SiraNo, rule.Amac, rule.KontrolTuru,
                rule.NormalEsik, rule.AltEsik, rule.ReferansDegeri, rule.IzinSayisi, rule.ArdisikDonemSayisi,
                rule.IzinTuru, rule.DegerlendirmeTuru, rule.Aciklama, rule.EtkinMi }).ToListAsync(ct);
}
