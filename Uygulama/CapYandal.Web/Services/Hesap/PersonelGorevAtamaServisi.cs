using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Hesap;

public enum PersonelGorevAtamaSonucu { Basarili, Gecersiz, IzinYok, Cakisma, Basarisiz }

// İlk görev verilirken rol, hesap durumu ve işlem geçmişi birlikte kaydedilir.
public sealed class PersonelGorevAtamaServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<PersonelGorevAtamaServisi> logger)
{
    public async Task<PersonelGorevAtamaSonucu> IlkGoreviAtaAsync(Guid actorId, PersonelGorevAtaModeli model,
        CancellationToken ct = default)
    {
        if (db.ChangeTracker.HasChanges()) return PersonelGorevAtamaSonucu.Basarisiz;
        if (actorId == Guid.Empty || model.KullaniciId == Guid.Empty || actorId == model.KullaniciId ||
            model.Rol is not (UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani)) return PersonelGorevAtamaSonucu.Gecersiz;
        try
        {
            if (db.Database.CurrentTransaction is { } caller &&
                caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return PersonelGorevAtamaSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                // Kimlik sırasıyla alınan hesap kilitleri, aynı hedefe eşzamanlı atamayı sıraya alır.
                var locked = new Dictionary<Guid, UygulamaKullanicisi>();
                foreach (var id in new[] { actorId, model.KullaniciId }.Order())
                {
                    var user = await db.Users.FromSqlInterpolated(
                        $"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
                    if (user is null) return PersonelGorevAtamaSonucu.Cakisma;
                    await db.Entry(user).ReloadAsync(ct);
                    locked.Add(id, user);
                }
                var permission = await access.OkuAsync(locked[actorId]);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli ||
                    !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return PersonelGorevAtamaSonucu.IzinYok;
                var target = locked[model.KullaniciId];
                if (target.OgrenciNo is not null) return PersonelGorevAtamaSonucu.IzinYok;
                if (target.AktifMi || (await users.GetRolesAsync(target)).Count != 0) return PersonelGorevAtamaSonucu.Cakisma;
                if (await PersonelErisimKurali.HazirlananGorevAsync(db, target.Id, ct) != model.Rol)
                    return PersonelGorevAtamaSonucu.IzinYok;
                if (!target.EmailConfirmed || !await users.HasPasswordAsync(target) ||
                    target.ParolaDegisikligiZorunlu || target.GeciciParolaBitisAni is not null)
                    return PersonelGorevAtamaSonucu.Cakisma;
                var normalizedRole = users.NormalizeName(model.Rol);
                if (!await db.Roles.AnyAsync(r => r.NormalizedName == normalizedRole, ct))
                    return PersonelGorevAtamaSonucu.Cakisma;
                // Aynı göreve yapılan devirler sırayla işlenir.
                await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource={"CapYandal:PersonelGorevi:" + model.Rol}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000; IF @result<0 THROW 51000,N'Görev kilidi alınamadı.',1;", ct);
                var previousIds = await (from u in db.Users join m in db.UserRoles on u.Id equals m.UserId join r in db.Roles on m.RoleId equals r.Id
                    where u.AktifMi && u.OgrenciNo == null && r.NormalizedName == normalizedRole select u.Id).Distinct().OrderBy(x => x).ToListAsync(ct);
                foreach(var previousId in previousIds)
                {
                    var previous = await db.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={previousId}").SingleAsync(ct);
                    previous.AktifMi = false;
                    if (!(await users.RemoveFromRoleAsync(previous, model.Rol)).Succeeded || !(await users.UpdateSecurityStampAsync(previous)).Succeeded)
                        return PersonelGorevAtamaSonucu.Cakisma;
                    db.DegisiklikGecmisi.Add(new() { Id=Guid.NewGuid(), VarlikTuru="PersonelGorevAtama", VarlikAnahtari=previous.Id.ToString(),
                        Islem="PersonelGorevDevri", YapanKullaniciId=actorId, IslemAni=clock.GetUtcNow(),
                        OncekiDeger=JsonSerializer.Serialize(new {KullaniciId=previous.Id,Rol=model.Rol,AktifMi=true}),
                        YeniDeger=JsonSerializer.Serialize(new {KullaniciId=previous.Id,Rol=(string?)null,AktifMi=false,YeniGorevli=model.KullaniciId}) });
                }
                target.AktifMi = true;
                if (!(await users.AddToRoleAsync(target, model.Rol)).Succeeded ||
                    !(await users.RemoveAuthenticationTokenAsync(target, PersonelErisimKurali.Provider, PersonelErisimKurali.PendingRole)).Succeeded ||
                    !(await users.UpdateSecurityStampAsync(target)).Succeeded) return PersonelGorevAtamaSonucu.Cakisma;
                db.DegisiklikGecmisi.Add(new()
                {
                    Id = Guid.NewGuid(), VarlikTuru = "PersonelGorevAtama", VarlikAnahtari = target.Id.ToString(),
                    Islem = "IlkGorevAta", YapanKullaniciId = actorId, IslemAni = clock.GetUtcNow(),
                    OncekiDeger = JsonSerializer.Serialize(new { KullaniciId = target.Id, Rol = (string?)null, AktifMi = false }),
                    YeniDeger = JsonSerializer.Serialize(new { KullaniciId = target.Id, Rol = model.Rol, AktifMi = true })
                });
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true;
                return PersonelGorevAtamaSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    db.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateException) { return PersonelGorevAtamaSonucu.Cakisma; }
        catch (Exception error)
        {
            logger.LogWarning("Personel görevi atanamadı: {HataTuru}.", error.GetType().Name);
            return PersonelGorevAtamaSonucu.Basarisiz;
        }
    }
}
