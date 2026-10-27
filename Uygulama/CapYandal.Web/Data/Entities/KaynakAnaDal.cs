namespace CapYandal.Web.Data.Entities;

// Yeni öğrenciye sunulan kaynak bölümler; geçmiş program kayıtları değiştirilmez.
public class KaynakAnaDal
{
    public Guid Id { get; set; }
    public OgrenciProgramTuru Tur { get; set; }
    public string Ad { get; set; } = "";
    public bool EtkinMi { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
