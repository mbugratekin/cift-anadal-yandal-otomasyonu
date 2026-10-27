using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Belgeler;

public enum GenelBelgeKayitKodu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }
public sealed record GenelBelgeKayitSonucu(GenelBelgeKayitKodu Kod, Guid? GenelBelgeId = null, int? SurumNo = null);

// Genel belge sürümü ve işlem geçmişi birlikte kaydedilir.
public sealed class GenelBelgeKayitServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, BelgeDeposu depo, TimeProvider clock, ILogger<GenelBelgeKayitServisi> logger)
{
    private static readonly HashSet<string> Turler =
        ["Akademik takvim", "Protokoller", "Ders kayıt rehberi", "Kurallar ve yönergeler", "Ders kayıt formları"];

    public async Task<GenelBelgeKayitSonucu> KaydetAsync(Guid actorId, Guid? genelBelgeId,
        string? tur, byte[]? beklenenSurum, IFormFile? file, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || tur is null || !Turler.Contains(tur) || genelBelgeId == Guid.Empty ||
            (genelBelgeId is not null && beklenenSurum is not { Length: 8 }) ||
            (genelBelgeId is null && beklenenSurum is not null)) return new(GenelBelgeKayitKodu.Gecersiz);
        // Servis başka kaydedilmemiş değişiklikleri yanlışlıkla SaveChanges'e dahil etmez.
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return new(GenelBelgeKayitKodu.Basarisiz);
        var written = new List<object>();
        GenelBelgeTanimi? definition = null;
        bool completed = false;
        bool commitStarted = false, rollbackConfirmed = false;
        DepolananBelge? stored = null;
        var stage = "Transaction";
        try
        {
            var actor = await users.FindByIdAsync(actorId.ToString());
            if (actor is null) return new(GenelBelgeKayitKodu.IzinYok);
            await db.Entry(actor).ReloadAsync(ct);
            var permission = await access.OkuAsync(actor);
            if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                return new(GenelBelgeKayitKodu.IzinYok);
            if (genelBelgeId is { } precheckId)
            {
                var precheck = await db.GenelBelgeTanimlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == precheckId, ct);
                if (precheck is null) return new(GenelBelgeKayitKodu.Bulunamadi);
                if (precheck.Tur != tur) return new(GenelBelgeKayitKodu.Gecersiz);
                if (!precheck.RowVersion.SequenceEqual(beklenenSurum!)) return new(GenelBelgeKayitKodu.Cakisma);
                var precheckPrev = await db.Belgeler.Where(b => b.GenelBelgeId == precheckId && b.Kapsam == BelgeKapsami.Genel)
                    .Select(b => (int?)b.SurumNo).MaxAsync(ct) ?? 0;
                if (precheckPrev == int.MaxValue) return new(GenelBelgeKayitKodu.Gecersiz);
            }
            // Dosya tarandıktan sonra yetki ve kayıt bilgileri yeniden kontrol edilir.
            stage = "Dosya";
            stored = await depo.KaydetAsync(file, ct);
            rollbackConfirmed = true;
            stage = "Transaction";
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            rollbackConfirmed = false;
            try
            {
                await db.Entry(actor).ReloadAsync(ct);
                permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                    return new(GenelBelgeKayitKodu.IzinYok);
                int previous = 0;
                if (genelBelgeId is { } id)
                {
                    // Güncel nesne okunur; eski tracked nesneye güvenilmez. Ham sorgu parametrelidir.
                    definition = await db.GenelBelgeTanimlari
                        .FromSqlInterpolated($"SELECT * FROM [GenelBelgeTanimlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {id}").AsNoTracking()
                        .SingleOrDefaultAsync(ct);
                    if (definition is null) return new(GenelBelgeKayitKodu.Bulunamadi);
                    if (definition.Tur != tur) return new(GenelBelgeKayitKodu.Gecersiz);
                    if (!definition.RowVersion.SequenceEqual(beklenenSurum!)) return new(GenelBelgeKayitKodu.Cakisma);
                    var tracked = db.ChangeTracker.Entries<GenelBelgeTanimi>().SingleOrDefault(e => e.Entity.Id == id);
                    if (tracked is not null) tracked.State = EntityState.Detached;
                    previous = await db.Belgeler.Where(b => b.GenelBelgeId == id && b.Kapsam == BelgeKapsami.Genel)
                        .Select(b => (int?)b.SurumNo).MaxAsync(ct) ?? 0;
                    // Tur bileşik anahtar parçasıdır; EF'de değiştirilmiş işaretlenmez.
                    // Değer değişmeden SQL rowversion ilerler, beklenen sürüm WHERE'de de zorlanır.
                    var updated = await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE [GenelBelgeTanimlari] SET [Tur] = [Tur] WHERE [Id] = {id} AND [RowVersion] = {beklenenSurum!}", ct);
                    if (updated != 1) return new(GenelBelgeKayitKodu.Cakisma);
                }
                else
                {
                    definition = new() { Id = Guid.NewGuid(), Tur = tur };
                    db.GenelBelgeTanimlari.Add(definition); written.Add(definition);
                }
                if (previous == int.MaxValue) return new(GenelBelgeKayitKodu.Gecersiz);
                var next = previous + 1;
                var belge = new Belge
                {
                    Id = Guid.NewGuid(), Kapsam = BelgeKapsami.Genel, Tur = tur,
                    GenelBelgeId = definition.Id, SurumNo = next, Sha256 = stored.Sha256,
                    YukleyenKullaniciId = actorId, YuklemeAni = clock.GetUtcNow(),
                    DosyaAdi = file!.FileName, IcerikTuru = stored.Mime, BoyutBayt = stored.Boyut,
                    DepolamaAnahtari = stored.Anahtar, IcerikDurumu = BelgeIcerikDurumu.Mevcut
                };
                var audit = new DegisiklikGecmisi
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(GenelBelgeTanimi), VarlikAnahtari = definition.Id.ToString(),
                    Islem = "GenelBelgeSurumYuklendi", YapanKullaniciId = actorId, IslemAni = belge.YuklemeAni,
                    OncekiDeger = JsonSerializer.Serialize(new { SurumNo = previous }),
                    YeniDeger = JsonSerializer.Serialize(new { SurumNo = next, BelgeId = belge.Id, Tur = tur })
                };
                db.Belgeler.Add(belge); db.DegisiklikGecmisi.Add(audit); written.Add(belge); written.Add(audit);
                stage = "Metadata";
                await db.SaveChangesAsync(ct);
                stage = "Commit";
                commitStarted = true; await tx.CommitAsync(ct);
                completed = true;
                return new(GenelBelgeKayitKodu.Basarili, definition.Id, next);
            }
            finally
            {
                if (!completed)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    rollbackConfirmed = true;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidDataException error) { logger.LogWarning("Genel belge güvenlik kontrolünde reddedildi: {Neden}",error.Message); return new(GenelBelgeKayitKodu.Gecersiz); }
        catch (DbUpdateConcurrencyException) { return new(GenelBelgeKayitKodu.Cakisma); }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627)
        { return new(GenelBelgeKayitKodu.Cakisma); }
        catch (Exception ex)
        {
            logger.LogWarning("Genel belge kayıt işlemi tamamlanamadı. Aşama: {Asama}, tür: {HataTuru}, SQL kodu: {SqlKodu}. Dosya/commit durumu ayrıca incelenmelidir.",
                stage, ex.GetType().Name, (ex.GetBaseException() as SqlException)?.Number);
            return new(GenelBelgeKayitKodu.Basarisiz);
        }
        finally
        {
            if (!completed)
            {
                foreach (var row in written) db.Entry(row).State = EntityState.Detached;
                if (definition is not null) db.Entry(definition).State = EntityState.Detached;
                if (stored is not null && rollbackConfirmed && !commitStarted)
                {
                    try { depo.Kaldir(stored.Anahtar); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { logger.LogCritical("Geri alınan genel belge dosyası temizlenemedi. İşlem: {Kimlik}, Hata: {Tur}", stored.Anahtar, error.GetType().Name); }
                }
            }
        }
    }
}
