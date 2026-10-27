using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace CapYandal.Web.Security;

public sealed class OgrenciKullaniciValidator : IUserValidator<UygulamaKullanicisi>
{
    public Task<IdentityResult> ValidateAsync(UserManager<UygulamaKullanicisi> manager, UygulamaKullanicisi user)
    {
        if (string.IsNullOrWhiteSpace(user.UserName) || user.UserName.Any(char.IsWhiteSpace))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "KullaniciAdiBoslukIceriyor",
                Description = "Kullanıcı adında boşluk kullanılamaz."
            }));
        }

        if (user.OgrenciNo is null)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        var valid = user.OgrenciNo.Length == 9 &&
            user.OgrenciNo.All(character => character is >= '0' and <= '9') &&
            string.Equals(user.UserName, user.OgrenciNo, StringComparison.Ordinal) &&
            !user.ParolaDegisikligiZorunlu && user.GeciciParolaBitisAni is null;

        return Task.FromResult(valid ? IdentityResult.Success : IdentityResult.Failed(new IdentityError
        {
            Code = "GecersizOgrenciHesabi",
            Description = "Öğrenci kullanıcı adı dokuz rakamlı öğrenci numarasıyla aynı olmalıdır; geçici öğrenci parolası kullanılmaz."
        }));
    }
}
