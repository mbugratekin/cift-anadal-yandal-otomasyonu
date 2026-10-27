namespace CapYandal.Web.Services.Eposta;

public static class HesapEpostaMetinleri
{
    public static EpostaIletisi IlkParola(string address, string link) => new(address,
        "Selçuk Üniversitesi ÇAP / Yan Dal — öğrenci hesabınız",
        $"Selçuk Üniversitesi Teknoloji Fakültesi Bilgisayar Mühendisliği Çift Anadal / Yan Dal Otomasyonu\n\n" +
        $"Bölüm koordinatörünüz öğrenci hesabınızı oluşturdu. Bu ileti başvuru veya kabul kararı değildir. Parolanızı belirlemek için bağlantıyı açın:\n\n{link}\n\n" +
        "Bağlantı 24 saat geçerlidir ve tek kullanımlıktır. Parolanızı oluşturduğunuzda bu e-posta adresi doğrulanır. " +
        "Sonra öğrenci numaranız ve parolanızla normal giriş ekranından giriş yapın. " +
        "Bu kayıt size ait değilse bağlantıyı kullanmayın ve bölüm koordinatörünüzle iletişime geçin.");
    public static EpostaIletisi ParolaSifirlandi(string address) => new(address,
        "ÇAP / Yan Dal şifreniz değiştirildi",
        "Hesabınızın şifresi sıfırlama bağlantısı üzerinden değiştirildi. Eski oturumlarınız kapatıldı.\n\n" +
        "Bu işlemi siz yapmadıysanız uygulamanın normal giriş ekranından yeniden şifre sıfırlama isteyin " +
        "ve bölüm koordinatörünüzle iletişime geçin. Şifrenizi kimseyle paylaşmayın.");

    public static EpostaIletisi KisiselAdresDogrulama(string address, string link) => new(address,
        "ÇAP / Yan Dal kişisel e-posta doğrulama",
        $"Kişisel e-posta adresinizi doğrulamak için bağlantıyı açın ve açılan sayfada onaylayın:\n\n{link}\n\n" +
        "Bağlantı 24 saat geçerlidir ve tek kullanımlıktır. Bu isteği siz başlatmadıysanız iletiyi yok sayın.");

    public static EpostaIletisi ParolaSifirlama(string address, string link) => new(address,
        "ÇAP / Yan Dal parola sıfırlama",
        $"Parola sıfırlama isteğiniz alındı. Yeni parolanızı belirlemek için aşağıdaki tek kullanımlık bağlantıyı açın:\n\n{link}\n\n" +
        "Bağlantı 24 saat geçerlidir. Bu isteği siz başlatmadıysanız bu e-postayı yok sayabilirsiniz.");
}
