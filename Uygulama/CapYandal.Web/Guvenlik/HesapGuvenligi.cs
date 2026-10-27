using System.Threading.RateLimiting;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace CapYandal.Web.Security;

public static class HesapGuvenligi
{
    public const string GirisSiniri = "giris";

    public static IServiceCollection AddHesapGuvenligi(this IServiceCollection services)
    {
        services.AddIdentity<UygulamaKullanicisi, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 8;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<BoslukIcermezParolaDogrulayici>()
            .AddUserValidator<OgrenciKullaniciValidator>();

        services.AddScoped<HesapErisimi>();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
        services.AddScoped<HesapOturumu>();
        services.AddScoped<HesapKurulumuFiltresi>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-CapYandal";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/Hesap/Giris";
            options.AccessDeniedPath = "/Hesap/Yetkisiz";
            options.Events.OnValidatePrincipal = context =>
                context.HttpContext.RequestServices.GetRequiredService<HesapOturumu>().DogrulaAsync(context);
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(GirisSiniri, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsync(
                    "Çok fazla giriş denemesi yapıldı. Bir dakika sonra tekrar deneyin.", cancellationToken);
            };
        });
        return services;
    }
}
