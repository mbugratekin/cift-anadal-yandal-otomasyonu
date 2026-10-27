using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CapYandal.Web.Services.Eposta;

public sealed class EpostaGonderimOptions
{
    public const string SectionName = "EpostaGonderimi";

    public bool Etkin { get; init; }
    public string? Sunucu { get; init; }
    public int Port { get; init; }
    public EpostaSmtpTlsModu TlsModu { get; init; } = EpostaSmtpTlsModu.StartTls;
    public string? GonderenAdresi { get; init; }
    public string GonderenAdi { get; init; } = "ÇAP / Yan Dal";
    public string? UygulamaAdresi { get; init; }
    public string? KullaniciAdi { get; init; }
    public string? Parola { get; init; }
    public string? TestAlicisi { get; init; }
    public List<GeciciEpostaAlicisi> GeciciAlicilar { get; init; } = [];
    public TimeSpan ZamanAsimi { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed class GeciciEpostaAlicisi
{
    public string OkulAdresi { get; init; } = "";
    public string TeslimAdresi { get; init; } = "";
}

public enum EpostaSmtpTlsModu
{
    StartTls = 1,
    SslOnConnect = 2
}

public sealed class EpostaGonderimOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<EpostaGonderimOptions>
{
    public ValidateOptionsResult Validate(string? name, EpostaGonderimOptions options)
    {
        var errors = new List<string>();
        // Değerlendirme sitesi Staging çalışır; sentetik okul adresleri tek güvenli test kutusuna yönlenir.
        var isTestEnvironment = environment.IsDevelopment() || environment.IsStaging();

        if (!isTestEnvironment && (options.TestAlicisi is not null || options.GeciciAlicilar.Count > 0))
            errors.Add("Test alıcısı yalnız Development veya Staging ortamında kullanılabilir.");

        var mappings = options.GeciciAlicilar.Where(x => !string.IsNullOrEmpty(x.OkulAdresi) || !string.IsNullOrEmpty(x.TeslimAdresi)).ToArray();
        if (mappings.Any(x => !IsEmail(x.OkulAdresi) || !IsEmail(x.TeslimAdresi)) ||
            mappings.Select(x => x.OkulAdresi).Distinct(StringComparer.OrdinalIgnoreCase).Count() != mappings.Length)
            errors.Add("Geçici alıcı eşlemeleri geçerli ve okul adresleri benzersiz olmalıdır.");

        if (!options.Etkin)
            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);

        if (string.IsNullOrWhiteSpace(options.Sunucu) || Uri.CheckHostName(options.Sunucu) == UriHostNameType.Unknown)
            errors.Add("SMTP sunucusu zorunludur.");
        if (options.Port is < 1 or > 65535)
            errors.Add("SMTP portu 1 ile 65535 arasında olmalıdır.");
        if (!Enum.IsDefined(options.TlsModu))
            errors.Add("SMTP için yalnız StartTls veya SslOnConnect kullanılabilir.");
        if (!IsEmail(options.GonderenAdresi))
            errors.Add("Geçerli bir gönderici adresi zorunludur.");
        if (!IsHttpsApplicationAddress(options.UygulamaAdresi))
            errors.Add("Parola bağlantıları için HTTPS kullanan geçerli UygulamaAdresi zorunludur.");
        if (options.ZamanAsimi < TimeSpan.FromSeconds(1) || options.ZamanAsimi > TimeSpan.FromMinutes(2))
            errors.Add("SMTP zaman aşımı 1 saniye ile 2 dakika arasında olmalıdır.");
        if (string.IsNullOrWhiteSpace(options.KullaniciAdi) != string.IsNullOrWhiteSpace(options.Parola))
            errors.Add("SMTP kullanıcı adı ve parolası birlikte yapılandırılmalıdır.");
        if (isTestEnvironment && !IsEmail(options.TestAlicisi))
            errors.Add("Test ortamı gönderimi için geçerli tek TestAlicisi zorunludur.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }

    private static bool IsHttpsApplicationAddress(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);
    }
}
