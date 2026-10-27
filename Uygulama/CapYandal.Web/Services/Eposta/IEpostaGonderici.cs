namespace CapYandal.Web.Services.Eposta;

public interface IEpostaGonderici
{
    Task<EpostaGonderimSonucu> GonderAsync(EpostaIletisi ileti, CancellationToken cancellationToken = default);
}

public sealed record EpostaIletisi(string Alici, string Konu, string Govde);

public enum EpostaGonderimSonucu
{
    DevreDisi,
    SmtpSunucusuKabulEtti
}
