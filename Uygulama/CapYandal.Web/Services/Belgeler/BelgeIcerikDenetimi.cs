namespace CapYandal.Web.Services.Belgeler;

// Mevcut testlerde transaction sınırını gözleyen isteğe bağlı kanca.
// Üretimde bağlı tarayıcı yoktur; antivirüs veya tam PDF çözümlemesi yapılmaz.
public interface IBelgeZararliIcerikTaramasi
{
    Task TaraAsync(string yol, CancellationToken iptal);
}

public sealed class BelgeIcerikDenetimi(IBelgeZararliIcerikTaramasi? tarayici = null)
{
    public Task DogrulaAsync(string yol, CancellationToken iptal)
    {
        iptal.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(yol) || !File.Exists(yol))
            throw new InvalidDataException("Denetlenecek dosya bulunamadı.");
        return tarayici?.TaraAsync(yol, iptal) ?? Task.CompletedTask;
    }
}
