using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Dersler;

public sealed class MufredatYonetimiServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users, HesapErisimi access, TimeProvider clock, ILogger<MufredatYonetimiServisi> logger)
{
    public Task<SablonKayitSonucu> EkleAsync(Guid actor, MufredatEkleModel model, CancellationToken ct) =>
        YonetAsync(actor, model, null, ct);
    public Task<SablonKayitSonucu> DurumAsync(Guid actor, MufredatDurumModel model, CancellationToken ct) =>
        YonetAsync(actor, null, model, ct);
    private async Task<SablonKayitSonucu> YonetAsync(Guid actor, MufredatEkleModel? create, MufredatDurumModel? change, CancellationToken ct)
    {
        if (create is not null && (string.IsNullOrWhiteSpace(create.Ad) || create.Ad.Trim().Length > 100 || create.BaslangicYili is < 2 or > 9998) ||
            change is not null && (change.Id == Guid.Empty || change.EtkinMi is null || change.Surum.Length != 12)) return SablonKayitSonucu.Gecersiz;
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges()) return SablonKayitSonucu.Basarisiz;
        var initial = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance); bool complete = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var user = await users.FindByIdAsync(actor.ToString()); if (user is null) return SablonKayitSonucu.IzinYok;
                await db.Entry(user).ReloadAsync(ct); var permission = await access.OkuAsync(user);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator)) return SablonKayitSonucu.IzinYok;
                Mufredat item; string? before = null;
                if (create is not null)
                {
                    var name = create.Ad.Trim();
                    if (await db.Mufredatlar.AnyAsync(x => x.Ad == name || x.BaslangicYili == create.BaslangicYili, ct)) return SablonKayitSonucu.Cakisma;
                    item = new() { Id = Guid.NewGuid(), Ad = name, BaslangicYili = create.BaslangicYili, EtkinMi = true, OlusturanKullaniciId = actor, OlusturmaAni = clock.GetUtcNow() };
                    db.Mufredatlar.Add(item);
                    var sources = await db.KaynakAnaDallar.Where(x => x.Tur == OgrenciProgramTuru.Cap && x.EtkinMi)
                        .Select(x => x.Ad).ToListAsync(ct);
                    foreach (var (type, source) in sources.Select(x => (OgrenciProgramTuru.Cap, (string?)x)).Append((OgrenciProgramTuru.YanDal, (string?)null)))
                    {
                        var next = (await db.DersSablonlari.Where(x => x.Tur == type && x.HedefBolum == "Bilgisayar Mühendisliği" && x.KaynakAnaDal == source).MaxAsync(x => (int?)x.MufredatNo, ct) ?? 0) + 1;
                        db.DersSablonlari.Add(new() { Id = Guid.NewGuid(), MufredatId = item.Id, Tur = type, HedefBolum = "Bilgisayar Mühendisliği", KaynakAnaDal = source,
                            Ad = source ?? "Bilgisayar Mühendisliği", MufredatAdi = name, MufredatNo = next, EtkinMi = true, OlusturanKullaniciId = actor, OlusturulmaAni = item.OlusturmaAni });
                    }
                }
                else
                {
                    item = (await db.Mufredatlar.FromSqlInterpolated($"SELECT * FROM [Mufredatlar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={change!.Id}").SingleOrDefaultAsync(ct))!;
                    if (item is null) return SablonKayitSonucu.Bulunamadi;
                    if (!KayitSurumKurali.KomutGuncel(item.Id, change.Id, item.RowVersion, change.Surum)) return SablonKayitSonucu.Cakisma;
                    before = JsonSerializer.Serialize(new { item.Ad, item.BaslangicYili, item.EtkinMi }); item.EtkinMi = change.EtkinMi!.Value;
                }
                db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(Mufredat), VarlikAnahtari = item.Id.ToString(),
                    Islem = create is null ? "OrtakMufredatDurumu" : "OrtakMufredatEkle", YapanKullaniciId = actor, IslemAni = clock.GetUtcNow(), OncekiDeger = before,
                    YeniDeger = JsonSerializer.Serialize(new { item.Ad, item.BaslangicYili, item.EtkinMi }) });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); complete = true; return SablonKayitSonucu.Basarili;
            }
            finally { if (!complete) await tx.RollbackAsync(CancellationToken.None); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return SablonKayitSonucu.Cakisma; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return SablonKayitSonucu.Cakisma; }
        catch (Exception ex) { logger.LogWarning("Ortak müfredat kaydedilemedi: {HataTuru}.", ex.GetType().Name); return SablonKayitSonucu.Basarisiz; }
        finally { if (!complete) foreach (var e in db.ChangeTracker.Entries().Where(x => !initial.Contains(x.Entity)).ToArray()) e.State = EntityState.Detached; }
    }
}
