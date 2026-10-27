namespace CapYandal.Web.Data.Entities;

public enum GanoKontrolAmaci { Devam = 1, Tamamlama = 2 }
public enum GanoKontrolTuru { TekEsik = 1, DusmeHakki = 2, UstUsteDusukDonem = 3 }

// One complete, immutable CAP or minor save. An empty rule list is still a version.
public class AkademikKosulSurumu
{
    public Guid Id { get; set; }
    public OgrenciProgramTuru ProgramTuru { get; set; }
    public int SurumNo { get; set; }
    public bool TamamlandiMi { get; set; }
    public string SayisalKurallarJson { get; set; } = "[]";
    public Guid OlusturanKullaniciId { get; set; }
    public DateTimeOffset OlusturulmaAni { get; set; }
}
