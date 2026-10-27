using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace CapYandal.Web.Security;

// Form dışından yapılan Identity parola işlemlerinde de aynı kuralı korur.
public sealed class BoslukIcermezParolaDogrulayici : IPasswordValidator<UygulamaKullanicisi>
{
    public Task<IdentityResult> ValidateAsync(UserManager<UygulamaKullanicisi> manager,
        UygulamaKullanicisi user, string? password)
    {
        var result = password?.Any(char.IsWhiteSpace) == true
            ? IdentityResult.Failed(new IdentityError { Code = "PasswordContainsWhitespace", Description = "Parolada boşluk kullanılamaz." })
            : IdentityResult.Success;
        return Task.FromResult(result);
    }
}
