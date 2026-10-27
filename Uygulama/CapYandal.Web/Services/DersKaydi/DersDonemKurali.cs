using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services.DersKaydi;

// Dönem sırası — geçilme dönemi alınan dönemden önce veya işlem döneminden sonra olamaz.
public static class DersDonemKurali
{
    public static bool OnceMi(Donem a, Donem b) => a.AkademikYilBaslangici < b.AkademikYilBaslangici ||
        (a.AkademikYilBaslangici == b.AkademikYilBaslangici && (int)a.Yariyil < (int)b.Yariyil);

    public static bool GecilmeGecerli(Donem taken, Donem? passed, Donem? current) =>
        passed is not null && current is not null && !OnceMi(passed, taken) && !OnceMi(current, passed);
}
