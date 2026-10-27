using Microsoft.Extensions.Options;
using CapYandal.Web.Services.Hesap;

namespace CapYandal.Web.Services.Eposta;

public static class EpostaServisKayitlari
{
    public static IServiceCollection AddEpostaGonderimi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<EpostaGonderimOptions>, EpostaGonderimOptionsValidator>();
        services.AddOptions<EpostaGonderimOptions>()
            .Bind(configuration.GetSection(EpostaGonderimOptions.SectionName))
            .ValidateOnStart();
        services.AddTransient<IEpostaGonderici, SmtpEpostaGonderici>();
        services.AddScoped<ISifreSifirlamaServisi, SifreSifirlamaServisi>();
        services.AddScoped<IlkDavetServisi>();
        services.AddScoped<IKisiselEpostaKurulumServisi, KisiselEpostaKurulumServisi>();
        return services;
    }
}
