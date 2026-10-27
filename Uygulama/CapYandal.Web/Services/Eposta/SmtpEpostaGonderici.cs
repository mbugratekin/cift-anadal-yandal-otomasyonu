using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CapYandal.Web.Services.Eposta;

public sealed class SmtpEpostaGonderici(
    IOptions<EpostaGonderimOptions> options,
    IHostEnvironment environment) : IEpostaGonderici
{
    public static MimeMessage HazirlaMesaj(EpostaIletisi ileti, EpostaGonderimOptions settings, bool isTestEnvironment)
    {
        if (!isTestEnvironment && (settings.TestAlicisi is not null || settings.GeciciAlicilar.Count > 0))
            throw new InvalidOperationException("Production ortamında TestAlicisi kullanılamaz.");

        var alici = isTestEnvironment ? settings.TestAlicisi : ileti.Alici;
        if (isTestEnvironment)
        {
            var matches = settings.GeciciAlicilar.Where(x =>
                string.Equals(x.OkulAdresi, ileti.Alici, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Tek okul adresine birden fazla geçici alıcı atanamaz.");
            if (matches.Length == 1) alici = matches[0].TeslimAdresi;
        }
        if (string.IsNullOrWhiteSpace(alici) || string.IsNullOrWhiteSpace(ileti.Konu) || string.IsNullOrWhiteSpace(ileti.Govde))
            throw new ArgumentException("E-posta alıcısı, konusu ve gövdesi boş olamaz.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.GonderenAdi, settings.GonderenAdresi!));
        message.To.Add(MailboxAddress.Parse(alici));
        message.Subject = ileti.Konu;
        message.Body = new TextPart("plain") { Text = ileti.Govde };
        return message;
    }

    public async Task<EpostaGonderimSonucu> GonderAsync(EpostaIletisi ileti, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Etkin)
            return EpostaGonderimSonucu.DevreDisi;

        using var message = HazirlaMesaj(ileti, settings, environment.IsDevelopment() || environment.IsStaging());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.ZamanAsimi);
        var operationToken = timeout.Token;

        using var client = new MailKit.Net.Smtp.SmtpClient
        {
            Timeout = checked((int)settings.ZamanAsimi.TotalMilliseconds)
        };

        var tlsMode = settings.TlsModu switch
        {
            EpostaSmtpTlsModu.StartTls => SecureSocketOptions.StartTls,
            EpostaSmtpTlsModu.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("SMTP TLS modu geçersizdir.")
        };
        await client.ConnectAsync(settings.Sunucu!, settings.Port, tlsMode, operationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.KullaniciAdi))
                await client.AuthenticateAsync(settings.KullaniciAdi,
                    settings.Parola ?? throw new InvalidOperationException("SMTP parolası yapılandırılmalıdır."),
                    operationToken);

            await client.SendAsync(message, operationToken);
            return EpostaGonderimSonucu.SmtpSunucusuKabulEtti;
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(false, CancellationToken.None);
                }
                catch (Exception exception) when (exception is IOException or MailKit.ProtocolException
                    or MailKit.ServiceNotConnectedException or OperationCanceledException)
                {
                    // Gönderim sunucu tarafından kabul edildiyse cleanup hatası sonucu geri alamaz.
                }
            }
        }
    }
}
