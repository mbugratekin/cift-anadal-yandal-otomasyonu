using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Security;

public sealed class HesapOturumu(SignInManager<UygulamaKullanicisi> signIn, HesapErisimi access,
    Microsoft.Extensions.Options.IOptions<IdentityOptions> identityOptions)
{
    internal const string ErisimAnahtari = "CapYandal.HesapErisimi";

    public async Task DogrulaAsync(CookieValidatePrincipalContext context)
    {
        var user = await signIn.ValidateSecurityStampAsync(context.Principal);
        if (user is not null)
        {
            var state = await access.OkuAsync(user);
            var cookieRoles = context.Principal!.FindAll(identityOptions.Value.ClaimsIdentity.RoleClaimType)
                .Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
            if (state.Izinli && cookieRoles.SetEquals(state.Roller))
            {
                context.HttpContext.Items[ErisimAnahtari] = state;
                return;
            }
        }
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
