using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Belgeler;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Sohbet;
public enum SohbetSonucu { Basarili, Gecersiz, IzinYok, Cakisma, Basarisiz }

public sealed class SohbetServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<SohbetServisi> logger, BelgeDeposu? depo = null)
{
    private async Task<(bool Allowed, bool Coordinator)> YetkiAsync(Guid actorId, CancellationToken ct, bool kilitle = false)
    {
        var actor = kilitle ? await db.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actorId}").SingleOrDefaultAsync(ct)
            : await users.FindByIdAsync(actorId.ToString()); if (actor is null) return (false, false);
        await db.Entry(actor).ReloadAsync(ct); var p = await access.OkuAsync(actor);
        var coordinator = p.Roller.Contains(UygulamaRolleri.Koordinator);
        return (p.Izinli && !p.ParolaDegisikligiGerekli && p.Roller.Length == 1 && (coordinator || p.Roller.Contains(UygulamaRolleri.Ogrenci)), coordinator);
    }
    private Task<bool> HedefAsync(Guid id, CancellationToken ct) => (from p in db.OgrenciProgramlari.AsNoTracking()
        join u in db.Users.AsNoTracking() on p.OgrenciId equals u.Id
        where p.OgrenciId == id && p.HedefBolum == "Bilgisayar Mühendisliği" && u.OgrenciNo != null select p.Id).AnyAsync(ct);

    public async Task<SohbetPanelModel?> OkuAsync(Guid actor, Guid? studentId, int page, CancellationToken ct = default)
    {
        if (actor == Guid.Empty || page is < 0 or > 100000) return null;
        var permission = await YetkiAsync(actor, ct); if (!permission.Allowed) return null;
        if (!permission.Coordinator)
        {
            if (studentId.HasValue && studentId != actor) return null;
            studentId = actor;
        }
        var recipients = permission.Coordinator ? await (from u in db.Users.AsNoTracking()
            where u.OgrenciNo != null && db.OgrenciProgramlari.Any(p => p.OgrenciId == u.Id && p.HedefBolum == "Bilgisayar Mühendisliği")
            orderby u.OgrenciNo select new SohbetOgrenciModel(u.Id, u.OgrenciNo + " " + u.Ad + " " + u.Soyad) {
                ProgramId = db.OgrenciProgramlari.Where(p => p.OgrenciId == u.Id && p.HedefBolum == "Bilgisayar Mühendisliği").Select(p => p.Id).FirstOrDefault(),
                FotografVar = db.KullaniciFotograflari.Any(f => f.KullaniciId == u.Id),
                Okunmamis = db.SohbetMesajlari.Count(m => m.OgrenciId == u.Id && m.GonderenKullaniciId == u.Id && m.OkunmaAni == null && m.KaldirmaAni == null)
            }).ToListAsync(ct) : [];
        if (studentId is null) return new(null, true, 0, false, recipients, []);
        if (studentId == Guid.Empty || !await HedefAsync(studentId.Value, ct)) return null;
        var rows = await db.SohbetMesajlari.AsNoTracking().Where(m => m.OgrenciId == studentId && m.KaldirmaAni == null).OrderByDescending(m => m.GonderimAni).ThenByDescending(m => m.Id)
            .Skip(page * 50).Take(51).ToListAsync(ct);
        var older = rows.Count > 50; rows = rows.Take(50).Reverse().ToList();
        var messageIds = rows.Select(x => x.Id).ToArray();
        var attachments = await db.SohbetMesajEkleri.AsNoTracking().Where(x => messageIds.Contains(x.SohbetMesajiId)).ToDictionaryAsync(x => x.SohbetMesajiId, ct);
        var senders = rows.Select(m => m.GonderenKullaniciId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => senders.Contains(u.Id)).Select(u => new { u.Id, Ad = (u.Ad ?? "") + " " + (u.Soyad ?? ""), u.OgrenciNo })
            .ToDictionaryAsync(u => u.Id, ct);
        return new(studentId, permission.Coordinator, page, older, recipients, rows.Select(m => {
            var sender = names.GetValueOrDefault(m.GonderenKullaniciId);
            var attachment = attachments.GetValueOrDefault(m.Id);
            return new SohbetMesajModel(m.Id, string.IsNullOrWhiteSpace(sender?.Ad) ? sender?.OgrenciNo ?? "Koordinatör" : sender.Ad.Trim(), SunumGorunumu.SohbetMetni(m.Metin), m.GonderimAni, m.GonderenKullaniciId == actor)
                { SabitlendiMi = m.SabitlendiMi, Ek = attachment is null ? null : new(attachment.Id, attachment.DosyaAdi, attachment.BoyutBayt) };
        }).ToList());
    }

    public async Task<SohbetSonucu> GonderAsync(Guid actor, SohbetGonderModel input, CancellationToken ct = default)
    {
        var text = input.Metin?.Trim() ?? string.Empty;
        if (actor == Guid.Empty || input.IstekId == Guid.Empty || input.OgrenciId == Guid.Empty || (text.Length == 0 && input.Ek is null) || text.Length > 2000 ||
            text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) return SohbetSonucu.Gecersiz;
        if (db.ChangeTracker.HasChanges()) return SohbetSonucu.Basarisiz;
        if (input.Ek is not null && (depo is null || db.Database.CurrentTransaction is not null)) return SohbetSonucu.Basarisiz;
        SohbetMesaji? added = null; bool complete = false, commitStarted = false; DepolananBelge? stored = null;
        var written = new List<object>();
        try
        {
            if (db.Database.CurrentTransaction is { } ext && ext.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return SohbetSonucu.Basarisiz;
            if (input.Ek is not null && db.Database.CurrentTransaction is null)
            {
                var prePermission = await YetkiAsync(actor, ct, kilitle: false);
                if (!prePermission.Allowed || (!prePermission.Coordinator && input.OgrenciId != actor) || !await HedefAsync(input.OgrenciId, ct))
                    return SohbetSonucu.IzinYok;
                var preExisting = await db.SohbetMesajlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.IstekId, ct);
                if (preExisting is null)
                    stored = await depo!.KaydetAsync(input.Ek, ct);
            }
            bool txRolledBack = db.Database.CurrentTransaction is null;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            txRolledBack = false;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                var permission = await YetkiAsync(actor, ct, kilitle: true);
                if (!permission.Allowed || (!permission.Coordinator && input.OgrenciId != actor) || !await HedefAsync(input.OgrenciId, ct)) return SohbetSonucu.IzinYok;
                var existing = await db.SohbetMesajlari.FromSqlInterpolated($"SELECT * FROM [SohbetMesajlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {input.IstekId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (existing is not null)
                {
                    if (existing.KaldirmaAni is not null || existing.OgrenciId != input.OgrenciId || existing.GonderenKullaniciId != actor || existing.Metin != text) return SohbetSonucu.Cakisma;
                    var oldAttachment = await db.SohbetMesajEkleri.AsNoTracking().SingleOrDefaultAsync(x => x.SohbetMesajiId == existing.Id, ct);
                    if ((oldAttachment is null) != (input.Ek is null)) return SohbetSonucu.Cakisma;
                    if (input.Ek is not null)
                    {
                        var checkedFile = await BelgeDosyaOkuyucu.OkuAsync(input.Ek, ct);
                        if (!checkedFile.Basarili) return SohbetSonucu.Gecersiz;
                        if (oldAttachment!.DosyaAdi != input.Ek.FileName || oldAttachment.IcerikTuru != checkedFile.GuvenliMime ||
                            oldAttachment.BoyutBayt != checkedFile.Icerik!.LongLength || oldAttachment.Sha256 != Convert.ToHexString(SHA256.HashData(checkedFile.Icerik)))
                            return SohbetSonucu.Cakisma;
                        try { await using var checkedStored = await depo!.AcAsync(new(oldAttachment.DepolamaAnahtari, oldAttachment.IcerikTuru, oldAttachment.BoyutBayt, oldAttachment.Sha256), ct); }
                        catch (IOException) { return SohbetSonucu.Basarisiz; }
                        if (stored is not null)
                        {
                            try { depo!.Kaldir(stored.Anahtar); }
                            catch (IOException) { }
                            stored = null;
                        }
                    }
                }
                else
                {
                    added = new() { Id = input.IstekId, OgrenciId = input.OgrenciId, GonderenKullaniciId = actor, Metin = text, GonderimAni = clock.GetUtcNow() };
                    db.SohbetMesajlari.Add(added); written.Add(added);
                    SohbetMesajEki? attachment = null;
                    if (input.Ek is not null)
                    {
                        stored ??= await depo!.KaydetAsync(input.Ek, ct);
                        attachment = new() { Id = Guid.NewGuid(), SohbetMesajiId = added.Id, DosyaAdi = input.Ek.FileName,
                            DepolamaAnahtari = stored.Anahtar, IcerikTuru = stored.Mime, BoyutBayt = stored.Boyut, Sha256 = stored.Sha256, YuklemeAni = added.GonderimAni };
                        db.SohbetMesajEkleri.Add(attachment); written.Add(attachment);
                    }
                    var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(SohbetMesaji), VarlikAnahtari = added.Id.ToString(),
                        Islem = "MesajGonder", YapanKullaniciId = actor, IslemAni = added.GonderimAni,
                        YeniDeger = JsonSerializer.Serialize(new { added.Id, added.OgrenciId, added.GonderenKullaniciId, added.GonderimAni, EkId = attachment?.Id }) };
                    db.DegisiklikGecmisi.Add(audit); written.Add(audit);
                    await db.SaveChangesAsync(ct);
                }
                if (own is not null) { commitStarted = true; await own.CommitAsync(ct); }
                complete = true; return SohbetSonucu.Basarili;
            }
            finally
            {
                if (!complete)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    txRolledBack = true;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (InvalidDataException) { return SohbetSonucu.Gecersiz; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return SohbetSonucu.Cakisma; }
        catch (Exception ex) { logger.LogWarning("Sohbet işlemi tamamlanamadı. Tür: {HataTuru}", ex.GetType().Name); return SohbetSonucu.Basarisiz; }
        finally
        {
            if (!complete)
            {
                foreach (var row in written) db.Entry(row).State = EntityState.Detached;
                // Commit sonucu belirsizse dosya tutulur; yalnız kesin geri alınmış yeni dosya telafi edilir.
                if (!commitStarted && stored is not null)
                {
                    try { depo!.Kaldir(stored.Anahtar); }
                    catch (IOException) { logger.LogWarning("Mesaj metadata geri alındı; yeni ek dosyasının telafisi tamamlanamadı."); }
                }
            }
        }
    }

    // Ek indirme — güncel rol ve konuşma sahibi doğrulanır; dosya hash/boyut kontrolü özel depodadır.
    public async Task<SohbetEkIndirme?> EkOkuAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        if (actor == Guid.Empty || id == Guid.Empty || depo is null) return null;
        var permission = await YetkiAsync(actor, ct); if (!permission.Allowed) return null;
        var row = await (from file in db.SohbetMesajEkleri.AsNoTracking()
            join message in db.SohbetMesajlari.AsNoTracking() on file.SohbetMesajiId equals message.Id
            where file.Id == id && message.KaldirmaAni == null
            select new { File = file, message.OgrenciId }).SingleOrDefaultAsync(ct);
        if (row is null || (!permission.Coordinator && row.OgrenciId != actor) || !await HedefAsync(row.OgrenciId, ct)) return null;
        try
        {
            var stream = await depo.AcAsync(new(row.File.DepolamaAnahtari, row.File.IcerikTuru, row.File.BoyutBayt, row.File.Sha256), ct);
            return new(row.File.DosyaAdi, row.File.IcerikTuru, stream);
        }
        catch (IOException) { return null; }
        catch (InvalidDataException) { return null; }
        catch (FormatException) { return null; }
        catch (ArgumentException) { return null; }
    }

    // Okunmamış mesaj sayısı — yalnız güncel rolün görebildiği karşı taraf kayıtları sayılır.
    public async Task<int?> BildirimSayisiAsync(Guid actor, CancellationToken ct = default)
    {
        var permission = await YetkiAsync(actor, ct); if (!permission.Allowed) return null;
        var query = db.SohbetMesajlari.AsNoTracking().Where(x => x.KaldirmaAni == null && x.OkunmaAni == null);
        query = permission.Coordinator
            ? query.Where(x => x.GonderenKullaniciId == x.OgrenciId && db.OgrenciProgramlari.Any(p => p.OgrenciId == x.OgrenciId && p.HedefBolum == "Bilgisayar Mühendisliği"))
            : query.Where(x => x.OgrenciId == actor && x.GonderenKullaniciId != actor);
        return await query.CountAsync(ct);
    }

    // Yalnız ekranda görülen karşı taraf mesajları okundu olarak işaretlenir.
    public async Task<SohbetSonucu> OkunduAsync(Guid actor, SohbetOkunduModeli input, CancellationToken ct = default)
    {
        if (actor == Guid.Empty || input.OgrenciId == Guid.Empty || input.Mesajlar is null || input.Mesajlar.Length is < 1 or > 50 ||
            input.Mesajlar.Any(x => x == Guid.Empty) || input.Mesajlar.Distinct().Count() != input.Mesajlar.Length) return SohbetSonucu.Gecersiz;
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return SohbetSonucu.Basarisiz;
        var written = new List<object>(); var completed = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var permission = await YetkiAsync(actor, ct, true);
            if (!permission.Allowed || (!permission.Coordinator && input.OgrenciId != actor) || !await HedefAsync(input.OgrenciId, ct)) return SohbetSonucu.IzinYok;
            var rows = await db.SohbetMesajlari.FromSqlInterpolated($"SELECT * FROM [SohbetMesajlari] WITH (UPDLOCK,HOLDLOCK) WHERE [OgrenciId]={input.OgrenciId}")
                .Where(x => input.Mesajlar.Contains(x.Id) && x.OkunmaAni == null && x.KaldirmaAni == null &&
                    (permission.Coordinator ? x.GonderenKullaniciId == x.OgrenciId : x.GonderenKullaniciId != actor)).OrderBy(x => x.Id).ToListAsync(ct);
            if (rows.Count > 0)
            {
                var now = clock.GetUtcNow();
                foreach(var row in rows) { row.OkunmaAni = now; written.Add(row); }
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(SohbetMesaji), VarlikAnahtari = input.OgrenciId.ToString(),
                    Islem = "MesajlarOkundu", YapanKullaniciId = actor, IslemAni = now,
                    OncekiDeger = JsonSerializer.Serialize(rows.Select(x => new { x.Id, OkunmaAni = (DateTimeOffset?)null })),
                    YeniDeger = JsonSerializer.Serialize(rows.Select(x => new { x.Id, x.OkunmaAni })) };
                db.DegisiklikGecmisi.Add(audit); written.Add(audit); await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct); completed = true; return SohbetSonucu.Basarili;
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(Exception error) { logger.LogWarning("Mesaj okundu bilgisi kaydedilemedi: {HataTuru}.", error.GetType().Name); return SohbetSonucu.Basarisiz; }
        finally { if(!completed) foreach(var row in written) db.Entry(row).State = EntityState.Detached; }
    }

}
public sealed record SohbetEkIndirme(string DosyaAdi, string IcerikTuru, Stream Stream);
