namespace CapYandal.Web.Services.Hesap;

public enum KisiselEpostaTalepSonucu { Gonderildi, DevreDisi, IzinYok, Basarisiz }

public interface IKisiselEpostaKurulumServisi
{
    Task<KisiselEpostaTalepSonucu> TalepEtAsync(Guid userId, string address, CancellationToken cancellationToken = default);
    Task<string?> BekleyenAdresiOkuAsync(Guid userId, string? requestId, string? token, CancellationToken cancellationToken = default);
    Task<bool> DogrulaAsync(Guid userId, string? requestId, string? token, CancellationToken cancellationToken = default);
}
