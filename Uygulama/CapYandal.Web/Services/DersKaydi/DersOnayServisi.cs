using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.DersKaydi;

public enum DersOnaySonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }
public sealed record DersOnayGirdisi(Guid ListeId, string? Surum, DersSecimiDurumu Karar = DersSecimiDurumu.Onaylandi, string? Gerekce = null, bool GeriAl = false, bool Mazeretli = false);

// Koordinatör yalnız açık ders kayıt döneminde liste kararı verebilir.
public sealed partial class DersOnayServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<DersOnayServisi> logger)
{
    public Task<DersOnaySonucu> KararVerAsync(Guid actorId, DersOnayGirdisi input, CancellationToken ct = default) => OnaylaAsync(actorId, input, ct);
    public async Task<DersOnaySonucu> OnaylaAsync(Guid actorId, DersOnayGirdisi input, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || input.ListeId == Guid.Empty || input.Surum is not { Length: 12 } ||
            (input.GeriAl ? input.Karar != DersSecimiDurumu.OnayBekliyor : input.Karar is not (DersSecimiDurumu.Onaylandi or DersSecimiDurumu.Reddedildi or DersSecimiDurumu.IptalEdildi)) || input.Gerekce?.Length > 2000 ||
            ((input.Karar is DersSecimiDurumu.Reddedildi or DersSecimiDurumu.IptalEdildi) && string.IsNullOrWhiteSpace(input.Gerekce)))
            return DersOnaySonucu.Gecersiz;
        if (((input.GeriAl || input.Mazeretli) && string.IsNullOrWhiteSpace(input.Gerekce)) || input.GeriAl && input.Mazeretli) return DersOnaySonucu.Gecersiz;
        if (db.ChangeTracker.HasChanges()) return DersOnaySonucu.Basarisiz;
        var written = new List<object>(); bool complete = false;
        try
        {
            if (db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return DersOnaySonucu.Basarisiz;
            // READUNCOMMITTED yalnız bağları bulur; kabul kararı değildir. Çağıranın
            // Liste ve dönem kayıtları her zaman aynı sırayla kilitlenir.
            var links = await db.DersSecimleri.FromSqlInterpolated($"SELECT * FROM [DersSecimleri] WITH (READUNCOMMITTED) WHERE [Id] = {input.ListeId}")
                .AsNoTracking().Select(l => new { l.DonemId, l.ProgramId }).SingleOrDefaultAsync(ct);
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return DersOnaySonucu.IzinYok;
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                    return DersOnaySonucu.IzinYok;

                if (links is null) return DersOnaySonucu.Bulunamadi;
                var period = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {links.DonemId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {links.ProgramId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (period is null || program?.HedefBolum != "Bilgisayar Mühendisliği") return DersOnaySonucu.Bulunamadi;
                var gate = await db.DersKayitKapilari.FromSqlInterpolated($"SELECT * FROM [DersKayitKapilari] WITH (UPDLOCK,HOLDLOCK) WHERE [DonemId] = {period.Id}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                var gateOpen = gate is not null && KayitKapisiKurali.EsikleriUygula(KayitKapisiServisi.Durum(gate), clock.GetUtcNow()).AcikMi;
                if (input.Mazeretli ? gateOpen || !period.IslemDonemiMi : !gateOpen)
                    return DersOnaySonucu.Gecersiz;
                var list = await db.DersSecimleri.FromSqlInterpolated($"SELECT * FROM [DersSecimleri] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {input.ListeId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (list is null || list.ProgramId != links.ProgramId || list.DonemId != links.DonemId) return DersOnaySonucu.Cakisma;
                if (input.Mazeretli && (!program.AktifMi || !list.MazeretliMi || input.Karar != DersSecimiDurumu.Onaylandi)) return DersOnaySonucu.Gecersiz;
                if (!KayitSurumKurali.KomutGuncel(list.Id, input.ListeId, list.RowVersion, input.Surum)) return DersOnaySonucu.Cakisma;
                if (input.Karar == DersSecimiDurumu.Onaylandi && await db.DersDuzeltmeTalepleri.AnyAsync(x => x.ListeId == list.Id && x.KapatilmaAni == null, ct))
                    return DersOnaySonucu.Gecersiz;
                if (list.Durum == DersSecimiDurumu.Onaylandi && input.Karar == DersSecimiDurumu.Onaylandi)
                {
                    if (list.OnaylayanKullaniciId is null || list.OnayAni is null) return DersOnaySonucu.Gecersiz;
                    if (own is not null) await own.CommitAsync(ct);
                    complete = true; // Aynı sürümün tekrarı audit/sürüm üretmez.
                    return DersOnaySonucu.Basarili;
                }
                if (input.GeriAl ? list.Durum != DersSecimiDurumu.Onaylandi : !OnayGecisKurali.KararGecerli((KayitOnayDurumu)(int)list.Durum, (KayitOnayDurumu)(int)input.Karar, input.Gerekce))
                    return DersOnaySonucu.Gecersiz;
                var before = JsonSerializer.Serialize(new { list.Durum, list.OnaylayanKullaniciId, list.OnayAni, list.KararGerekcesi, list.OgrenciKilidi });
                var tracked = db.ChangeTracker.Entries<DersSecimi>().SingleOrDefault(e => e.Entity.Id == list.Id);
                if (tracked is not null) tracked.State = EntityState.Detached;
                db.Attach(list); written.Add(list);
                var now = clock.GetUtcNow();
                var items = await db.DersSecimKalemleri.AsNoTracking().Where(x => x.ListeId == list.Id).ToListAsync(ct);
                if (await DersListeSurumKaydi.EskiyiKoruAsync(db, list, items, actorId, now, ct) is { } oldCopy) written.Add(oldCopy);
                list.OgrenciKilidi |= list.Durum is DersSecimiDurumu.Onaylandi or DersSecimiDurumu.IptalEdildi || input.Karar is DersSecimiDurumu.Onaylandi or DersSecimiDurumu.IptalEdildi;
                list.Durum = input.Karar; list.OnaylayanKullaniciId = actorId; list.OnayAni = now;
                list.KararGerekcesi = string.IsNullOrWhiteSpace(input.Gerekce) ? null : input.Gerekce.Trim();
                if (input.GeriAl) { list.OnaylayanKullaniciId = null; list.OnayAni = null; list.KararGerekcesi = null; }
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(DersSecimi), VarlikAnahtari = list.Id.ToString(),
                    Islem = input.GeriAl ? "DersListesiOnayiniGeriAl" : input.Mazeretli ? "MazeretliDersKaydiOnayla" : input.Karar == DersSecimiDurumu.Onaylandi ? "DersListesiOnayla" : input.Karar == DersSecimiDurumu.Reddedildi ? "DersListesiReddet" : "DersListesiIptal", OncekiDeger = before,
                    YeniDeger = JsonSerializer.Serialize(new { list.Durum, list.OnaylayanKullaniciId, list.OnayAni, list.KararGerekcesi, list.OgrenciKilidi, Gerekce = input.Gerekce }),
                    YapanKullaniciId = actorId, IslemAni = now };
                db.DegisiklikGecmisi.Add(audit); written.Add(audit);
                written.Add(await DersListeSurumKaydi.EkleAsync(db, list, items, actorId, now, input.Gerekce, ct));
                await db.SaveChangesAsync(ct);
                if (own is not null) await own.CommitAsync(ct);
                complete = true; return DersOnaySonucu.Basarili;
            }
            finally
            {
                if (!complete)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await own.RollbackAsync(CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return DersOnaySonucu.Cakisma; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return DersOnaySonucu.Cakisma; }
        catch (Exception ex)
        {
            logger.LogWarning("Ders onayı tamamlanamadı. Tür: {HataTuru}, SQL: {Kod}", ex.GetType().Name, (ex.GetBaseException() as SqlException)?.Number);
            return DersOnaySonucu.Basarisiz;
        }
        finally { if (!complete) foreach (var row in written) db.Entry(row).State = EntityState.Detached; }
    }
}
