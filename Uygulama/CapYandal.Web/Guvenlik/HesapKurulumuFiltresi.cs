using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapYandal.Web.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HesapKurulumunaIzinAttribute : Attribute { }

public sealed class HesapKurulumuFiltresi : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var metadata = context.HttpContext.GetEndpoint()?.Metadata;
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            if (context.HttpContext.Items[HesapOturumu.ErisimAnahtari] is not HesapErisimDurumu { Izinli: true } state)
            {
                context.Result = new ForbidResult();
                return;
            }
            if (metadata?.GetMetadata<HesapKurulumunaIzinAttribute>() is not null)
            {
                await next();
                return;
            }
            if (state.ParolaDegisikligiGerekli)
            {
                context.Result = new RedirectToActionResult("ParolaDegistir", "Hesap", null);
                return;
            }
            if (state.OgrenciKurulumuGerekli)
            {
                context.Result = new RedirectToActionResult("Kurulum", "Hesap", null);
                return;
            }
        }
        await next();
    }
}
