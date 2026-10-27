using CapYandal.Web.Models;

namespace CapYandal.Web.Services.Hesap;

public interface ISifreSifirlamaServisi
{
    Task TalepEtAsync(string? kullaniciAdi, CancellationToken cancellationToken = default);

    Task<bool> BaglantiGecerliMiAsync(
        Guid userId,
        string? resetId,
        string? token,
        CancellationToken cancellationToken = default);

    Task<SifreSifirlamaSonucu> SifirlaAsync(
        SifreSifirlamaModel model,
        CancellationToken cancellationToken = default);
}

public enum SifreSifirlamaSonucu
{
    Basarili,
    BaglantiGecersiz,
    ParolaKosullariUygunDegil
}
