using Microsoft.AspNetCore.Identity;

namespace CapYandal.Web.Data.Entities;

public class UygulamaKullanicisi : IdentityUser<Guid>
{
    public string? OgrenciNo { get; set; }
    public string? Ad { get; set; }
    public string? Soyad { get; set; }
    public string? KisiselEposta { get; set; }
    public string? BekleyenKisiselEposta { get; set; }
    public bool KisiselEpostaOnayliMi { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTimeOffset? IlkGirisAni { get; set; }
    public DateTimeOffset? SonGirisAni { get; set; }
    public bool ParolaDegisikligiZorunlu { get; set; }
    public DateTimeOffset? GeciciParolaBitisAni { get; set; }
}
