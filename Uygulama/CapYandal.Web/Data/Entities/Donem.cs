namespace CapYandal.Web.Data.Entities;

public class Donem
{
    public Guid Id { get; set; }
    public int AkademikYilBaslangici { get; set; }
    public AkademikYariyil Yariyil { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public DateOnly? BaslangicTarihi { get; set; }
    public DateOnly? BitisTarihi { get; set; }
    public bool IslemDonemiMi { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
