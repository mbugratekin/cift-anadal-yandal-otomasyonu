using System.Data;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Hesap;

public sealed record GorusmeCevabi(int Durum, string? Surum = null);
public sealed class GorusmeBilgisiServisi(AppDbContext db,
    HesapErisimi access, TimeProvider clock, ILogger<GorusmeBilgisiServisi> logger)
{
    public async Task<GorusmeCevabi> KaydetAsync(Guid actor, GorusmeGirdisi input, CancellationToken ct = default)
    {
        var email = input.IletisimEpostasi?.Trim();
        if (!string.IsNullOrEmpty(email) && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))) return new(400);
        if (actor == Guid.Empty || input.Blok is null || input.Oda is null || input.Blok.Length > 80 || input.Oda.Length > 80 ||
            input.Araliklar is null || input.Araliklar.Count > 32 || input.Araliklar.Any(x => x is null || !x.Gecerli) ||
            input.Araliklar.Distinct().Count() != input.Araliklar.Count) return new(400);
        byte[]? expected = null;
        if (input.Surum is not null)
        {
            try { expected = Convert.FromBase64String(input.Surum); } catch (FormatException) { return new(400); }
            if (expected.Length != 8) return new(400);
        }
        if (db.ChangeTracker.HasChanges()) return new(503);
        if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return new(503);
        try
        {
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var savepoint = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(savepoint, ct);
            bool completed = false;
            try
            {
                // Görüşme bilgisi — hesap kilidi altında güncel koordinatör yetkisi doğrulanır.
                var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor}").SingleOrDefaultAsync(ct);
                if (user is null) return new(403);
                await db.Entry(user).ReloadAsync(ct);
                var permission = await access.OkuAsync(user);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || user.OgrenciNo is not null ||
                    user.PasswordHash is null || user.GeciciParolaBitisAni is not null ||
                    !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return new(403);
                var record = await db.PersonelGorusmeBilgileri.FromSqlInterpolated($"SELECT * FROM [PersonelGorusmeBilgileri] WITH (UPDLOCK,HOLDLOCK) WHERE [KullaniciId]={actor}").SingleOrDefaultAsync(ct);
                if (record is not null) await db.Entry(record).ReloadAsync(ct);
                if (record is null ? expected is not null : expected is null || !expected.SequenceEqual(record.RowVersion)) return new(409);
                var previous = record is null ? null : JsonSerializer.Serialize(new { record.Blok, record.Oda, record.AraliklarJson, record.IletisimEpostasi });
                if (record is null) { record = new() { KullaniciId = actor }; db.PersonelGorusmeBilgileri.Add(record); }
                record.Blok = input.Blok.Trim(); record.Oda = input.Oda.Trim();
                // Eski istemciler adres göndermiyorsa mevcut iletişim adresini koru.
                if (input.IletisimEpostasi is not null) record.IletisimEpostasi = string.IsNullOrEmpty(email) ? null : email;
                record.AraliklarJson = JsonSerializer.Serialize(input.Araliklar.OrderBy(x => x.Gun).ThenBy(x => x.Baslangic));
                record.DegisiklikAni = clock.GetUtcNow();
                db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = "PersonelGorusmeBilgisi", VarlikAnahtari = actor.ToString(),
                    Islem = "Kaydet", YapanKullaniciId = actor, IslemAni = record.DegisiklikAni, OncekiDeger = previous,
                    YeniDeger = JsonSerializer.Serialize(new { record.Blok, record.Oda, record.AraliklarJson, record.IletisimEpostasi }) });
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true; return new(200, Convert.ToBase64String(record.RowVersion));
            }
            finally
            {
                if (!completed) { if (own is null) await tx.RollbackToSavepointAsync(savepoint, CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateException) { return new(409); }
        catch (Exception error) { logger.LogWarning("Görüşme bilgisi kaydedilemedi: {Tur}", error.GetType().Name); return new(503); }
    }

    public async Task<IReadOnlyList<GorusmeBilgisiModel>> OkuAsync(Guid actor, CancellationToken ct = default, bool sohbetAdresi = false)
    {
        var current = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, ct);
        if (current is null) return [];
        var permission = await access.OkuAsync(current);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli || permission.Roller.Length != 1 ||
            permission.Roller[0] is not (UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani)) return [];
        var people = await (from user in db.Users.AsNoTracking()
            join membership in db.UserRoles on user.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            where user.OgrenciNo == null && user.AktifMi && user.EmailConfirmed && user.PasswordHash != null &&
                !user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni == null &&
                (role.Name == UygulamaRolleri.Koordinator || role.Name == UygulamaRolleri.BolumBaskani)
            select new { user.Id, user.Ad, user.Soyad, user.Email, Role = role.Name }).ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();
        var stored = await db.PersonelGorusmeBilgileri.AsNoTracking().Where(x => ids.Contains(x.KullaniciId)).ToDictionaryAsync(x => x.KullaniciId, ct);
        var result = new List<GorusmeBilgisiModel>();
        foreach (var person in people.OrderBy(p => p.Role == UygulamaRolleri.Koordinator ? 0 : 1).ThenBy(p => p.Ad).ThenBy(p => p.Id))
        {
            if (await db.UserRoles.CountAsync(x => x.UserId == person.Id, ct) != 1) continue;
            stored.TryGetValue(person.Id, out var item);
            IReadOnlyList<GorusmeAraligi> ranges = [];
            if (item is not null)
            {
                try { ranges = JsonSerializer.Deserialize<List<GorusmeAraligi>>(item.AraliklarJson) ?? []; }
                catch (JsonException) { logger.LogWarning("Görüşme aralıkları okunamadı."); }
                if (ranges.Any(x => x is null || !x.Gecerli)) ranges = [];
            }
            // Sohbet başlığında yalnız koordinatörün kurumsal adresi gösterilir.
            result.Add(new(person.Id, $"{person.Ad} {person.Soyad}".Trim(), person.Role == UygulamaRolleri.Koordinator ? "Koordinatör" : "Bölüm Başkanı",
                item?.Blok ?? "", item?.Oda ?? "", ranges, person.Id == actor && item is not null ? Convert.ToBase64String(item.RowVersion) : null)
                { IletisimEpostasi = sohbetAdresi && person.Role == UygulamaRolleri.Koordinator ? item?.IletisimEpostasi ?? person.Email : null });
        }
        return result;
    }
}
