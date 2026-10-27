using CapYandal.Web.Data;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Paneller;

// Hesap yönetimi listesi — görevli ve hazırlık hesapları koordinatörün güncel yetkisiyle okunur.
public sealed class PersonelHesapPanelServisi(AppDbContext database, HesapErisimi access)
{
    public async Task<IReadOnlyList<PersonelGorevSecenegi>?> OkuAsync(Guid actorId, CancellationToken ct = default)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        if (actor is null) return null;
        var permission = await access.OkuAsync(actor);
        if (!permission.Izinli || permission.ParolaDegisikligiGerekli ||
            !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return null;

        var active = await (from user in database.Users.AsNoTracking()
            join membership in database.UserRoles.AsNoTracking() on user.Id equals membership.UserId
            join role in database.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where user.OgrenciNo == null && user.AktifMi && user.EmailConfirmed && user.PasswordHash != null &&
                !user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni == null &&
                (role.Name == UygulamaRolleri.Koordinator || role.Name == UygulamaRolleri.BolumBaskani) &&
                database.UserRoles.Count(r => r.UserId == user.Id) == 1
            orderby user.Ad, user.Soyad
            select new PersonelGorevSecenegi(user.Id, ((user.Ad ?? "") + " " + (user.Soyad ?? "")).Trim() == ""
                ? user.UserName! : ((user.Ad ?? "") + " " + (user.Soyad ?? "")).Trim(), role.Name!)).ToListAsync(ct);
        var pending = await (from user in database.Users.AsNoTracking()
            join token in database.UserTokens.AsNoTracking() on user.Id equals token.UserId
            where user.OgrenciNo == null && !user.AktifMi && !database.UserRoles.Any(r => r.UserId == user.Id) &&
                token.LoginProvider == "CapYandal" && token.Name == "PersonelHazirlananGorev" &&
                (token.Value == UygulamaRolleri.Koordinator || token.Value == UygulamaRolleri.BolumBaskani)
            orderby user.Ad, user.Soyad
            select new PersonelGorevSecenegi(user.Id, ((user.Ad ?? "") + " " + (user.Soyad ?? "")).Trim(), token.Value!)
            { GorevliMi = false, HazirMi = user.EmailConfirmed && user.PasswordHash != null &&
                !user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni == null }).ToListAsync(ct);
        return active.Concat(pending).ToList();
    }
}
