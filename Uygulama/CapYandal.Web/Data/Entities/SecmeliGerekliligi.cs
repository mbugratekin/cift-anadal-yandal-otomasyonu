namespace CapYandal.Web.Data.Entities;

public class SecmeliGerekliligi
{
    public Guid Id { get; set; }
    public Guid SablonId { get; set; }
    public int MufredatYariyili { get; set; }
    public DersTuru Tur { get; set; }
    public int GerekenSayi { get; set; }
}
