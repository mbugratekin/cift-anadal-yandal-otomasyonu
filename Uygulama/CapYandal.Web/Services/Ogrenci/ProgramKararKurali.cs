using CapYandal.Web.Data.Entities;

namespace CapYandal.Web.Services.Ogrenci;

// Karar kapsamı — akademik başarı hesaplanmaz; seçilen kararın program türü doğrulanır.
public static class ProgramKararKurali
{
    public static bool UygunMu(OgrenciProgramTuru tur, ProgramKararTuru karar) => (tur, karar) switch
    {
        (OgrenciProgramTuru.Cap, ProgramKararTuru.Mezuniyet or ProgramKararTuru.KayitKapatma) => true,
        (OgrenciProgramTuru.YanDal, ProgramKararTuru.Sertifika or ProgramKararTuru.KayitKapatma) => true,
        _ => false
    };

    public static string Ad(ProgramKararTuru? karar) => karar switch
    {
        ProgramKararTuru.Mezuniyet => "Mezuniyet",
        ProgramKararTuru.Sertifika => "Sertifika",
        ProgramKararTuru.KayitKapatma => "Kayıt kapatma",
        _ => "Karar yok"
    };
}
