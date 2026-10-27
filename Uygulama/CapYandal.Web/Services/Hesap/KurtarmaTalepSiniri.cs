using System.Globalization;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Hesap;

// Parola kurtarma istek sayısını aynı veritabanı işlemi içinde sınırlar.
internal static class KurtarmaTalepSiniri
{
    internal const string Anahtar="SonKurtarmaTalepAni";
    internal static async Task<bool> AyirAsync(AppDbContext db,UserManager<UygulamaKullanicisi> users,
        UygulamaKullanicisi user,DateTimeOffset now,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null) throw new InvalidOperationException("Kurtarma talebi transaction gerektirir.");
        var previous=await db.UserTokens.AsNoTracking().Where(t=>t.UserId==user.Id && t.LoginProvider=="CapYandal" && t.Name==Anahtar)
            .Select(t=>t.Value).SingleOrDefaultAsync(ct);
        if(DateTimeOffset.TryParseExact(previous,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var last) && now-last<TimeSpan.FromMinutes(1))
            return false;
        if(!(await users.SetAuthenticationTokenAsync(user,"CapYandal",Anahtar,now.ToString("O",CultureInfo.InvariantCulture))).Succeeded)
            throw new InvalidOperationException("Kurtarma talebi sınırı kaydedilemedi.");
        return true;
    }
}
