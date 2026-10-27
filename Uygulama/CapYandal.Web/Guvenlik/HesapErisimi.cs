using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Security;

public sealed record HesapErisimDurumu(bool Izinli, string[] Roller, bool OgrenciKurulumuGerekli, bool ParolaDegisikligiGerekli);

public sealed class HesapErisimi(AppDbContext database, UserManager<UygulamaKullanicisi> users, TimeProvider? clock = null)
{
    // Rol ve program durumu her istekte yeniden kontrol edilir.
    public async Task<HesapErisimDurumu> OkuAsync(UygulamaKullanicisi user)
    {
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        // Hazırlık kaydı yalnız ilk parola işlemi için kullanılır.
        if (roles.Length == 0 && user.OgrenciNo is null && !user.AktifMi && user.EmailConfirmed &&
            user.PasswordHash is not null && user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni > now &&
            await Services.Hesap.PersonelErisimKurali.HazirlananGorevAsync(database, user.Id) is not null)
            return new(true, [], false, true);
        if (!UygulamaRolleri.GecerliTekRol(roles)) return new(false, roles, false, false);
        var student = roles.Contains(UygulamaRolleri.Ogrenci);
        // Öğrencinin aktifliği programdan, personelin aktifliği hesaptan alınır.
        var allowed = (student || user.AktifMi) && user.EmailConfirmed;
        if (student)
        {
            allowed &= user.OgrenciNo is { Length: 9 } &&
                user.OgrenciNo.All(character => character is >= '0' and <= '9') &&
                string.Equals(user.UserName, user.OgrenciNo, StringComparison.Ordinal) &&
                !user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni is null;
            allowed &= await database.OgrenciProgramlari.AnyAsync(program => program.OgrenciId == user.Id && program.AktifMi && program.HedefBolum == "Bilgisayar Mühendisliği");
        }
        else if (user.OgrenciNo is not null)
            allowed = false;
        if (user.ParolaDegisikligiZorunlu &&
            (user.GeciciParolaBitisAni is null || user.GeciciParolaBitisAni <= now))
            allowed = false;

        return new(allowed, roles, false,
            !student && user.ParolaDegisikligiZorunlu);
    }
}
