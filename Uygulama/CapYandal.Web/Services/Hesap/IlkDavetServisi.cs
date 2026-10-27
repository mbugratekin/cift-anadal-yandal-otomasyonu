using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Eposta;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CapYandal.Web.Services.Hesap;

public enum IlkDavetSonucu { Gonderildi, DevreDisi, IzinYok, UygunDegil, Basarisiz }

// İlk parola bağlantısı, şifre sıfırlama bağlantısından ayrıdır.
public sealed class IlkDavetServisi(UserManager<UygulamaKullanicisi> users, AppDbContext database,
    HesapErisimi access, IEpostaGonderici sender, IOptions<EpostaGonderimOptions> options,
    ILogger<IlkDavetServisi> logger)
{
    private const string Provider = "CapYandal";
    private const string ActiveId = "AktifIlkDavetId";
    private const string DeliveredId = "SonBasariliIlkDavetId";
    private static string Purpose(UygulamaKullanicisi user, string id) => $"IlkParola:{id}:{user.Email}";

    public async Task<IlkDavetSonucu> GonderAsync(Guid actorId, string? ogrenciNo, CancellationToken ct = default)
    {
        if (ogrenciNo is not { Length: 9 } || !ogrenciNo.All(c => c is >= '0' and <= '9'))
            return IlkDavetSonucu.UygunDegil;
        if (database.ChangeTracker.HasChanges()) return IlkDavetSonucu.Basarisiz;
        Guid targetId = Guid.Empty;
        string? newId = null;
        try
        {
            string address;
            string link;
            await using (var owned = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null)
            {
                var transaction = owned ?? database.Database.CurrentTransaction!;
                var savepoint = Guid.NewGuid().ToString("N");
                if (owned is null) await transaction.CreateSavepointAsync(savepoint, ct);
                bool prepared = false; UygulamaKullanicisi? target = null; DegisiklikGecmisi? audit = null;
                try
                {
                    var actor = await users.FindByIdAsync(actorId.ToString());
                    if (actor is null) return IlkDavetSonucu.IzinYok;
                    await database.Entry(actor).ReloadAsync(ct);
                    var permission = await access.OkuAsync(actor);
                    if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                        return IlkDavetSonucu.IzinYok;
                    var user = await users.FindByNameAsync(ogrenciNo);
                    if (user is null) return IlkDavetSonucu.UygunDegil;
                    target = user;
                    await database.Entry(user).ReloadAsync(ct);
                    if (!await UygunMuAsync(user, ct)) return IlkDavetSonucu.UygunDegil;
                    if (!options.Value.Etkin) return IlkDavetSonucu.DevreDisi;
                    if (!Uri.TryCreate(options.Value.UygulamaAdresi, UriKind.Absolute, out var appUri) || appUri.Scheme != "https")
                        return IlkDavetSonucu.Basarisiz;
                    var hadInvitation = await AktifIdOkuAsync(user.Id, ct) is not null;
                    var id = Guid.NewGuid().ToString("N");
                    var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, Purpose(user, id));
                    if (!(await users.SetAuthenticationTokenAsync(user, Provider, ActiveId, id)).Succeeded)
                        return IlkDavetSonucu.Basarisiz;
                    // Davet bağlantısı hazırlanırken işlem geçmişine sır yazılmaz.
                    audit = new() { Id = Guid.NewGuid(), VarlikTuru = nameof(UygulamaKullanicisi), VarlikAnahtari = user.Id.ToString(),
                        Islem = "IlkDavetTalebi", YapanKullaniciId = actorId, IslemAni = DateTimeOffset.UtcNow,
                        OncekiDeger = JsonSerializer.Serialize(new { OncekiDavetVardi = hadInvitation }),
                        YeniDeger = "{\"TalepHazirlandi\":true}" };
                    database.DegisiklikGecmisi.Add(audit); await database.SaveChangesAsync(ct);
                    address = user.Email!;
                    link = QueryHelpers.AddQueryString(appUri.AbsoluteUri.TrimEnd('/') + "/Hesap/ParolaOlustur",
                        new Dictionary<string, string?>
                        {
                            [nameof(IlkParolaModel.UserId)] = user.Id.ToString(),
                            [nameof(IlkParolaModel.DavetId)] = id,
                            [nameof(IlkParolaModel.Token)] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
                        });
                    // Veritabanı kaydı tamamlanmadan davet gönderilmez.
                    if (owned is null) return IlkDavetSonucu.Basarisiz;
                    await owned.CommitAsync(ct);
                    targetId = user.Id; newId = id; prepared = true;
                }
                finally
                {
                    if (!prepared)
                    {
                        if (owned is null) await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                        else { await transaction.RollbackAsync(CancellationToken.None); await owned.DisposeAsync(); }
                        if (audit is not null) database.Entry(audit).State = EntityState.Detached;
                        if (target is not null)
                        {
                            foreach (var tracked in database.ChangeTracker.Entries<IdentityUserToken<Guid>>()
                                .Where(x => x.Entity.UserId == target.Id && x.Entity.LoginProvider == Provider && x.Entity.Name == ActiveId).ToArray())
                                tracked.State = EntityState.Detached;
                            if (database.Entry(target).State != EntityState.Detached) await database.Entry(target).ReloadAsync(CancellationToken.None);
                        }
                    }
                }
            }
            if (await sender.GonderAsync(HesapEpostaMetinleri.IlkParola(address, link), ct) == EpostaGonderimSonucu.SmtpSunucusuKabulEtti)
            {
                await BasariliDavetiKaydetAsync(targetId, newId!);
                return IlkDavetSonucu.Gonderildi;
            }
            await BasarisizDavetiGeriAlAsync(targetId, newId);
            return IlkDavetSonucu.DevreDisi;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await BasarisizDavetiGeriAlAsync(targetId, newId);
            throw;
        }
        catch (Exception)
        {
            await BasarisizDavetiGeriAlAsync(targetId, newId);
            logger.LogWarning("İlk parola daveti gönderilemedi; öğrenci kaydı korunur.");
            return IlkDavetSonucu.Basarisiz;
        }
    }

    public async Task<bool> GecerliMiAsync(Guid userId, string? davetId, string? token, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || !IdGecerli(davetId) || !Decode(token, out var decoded)) return false;
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return false;
        await database.Entry(user).ReloadAsync(ct);
        return database.Entry(user).State != EntityState.Detached && await UygunMuAsync(user, ct) &&
            await AktifIdOkuAsync(user.Id, ct) == davetId &&
            await users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, Purpose(user, davetId!), decoded);
    }

    private Task<string?> AktifIdOkuAsync(Guid userId, CancellationToken ct) => database.UserTokens.AsNoTracking()
        .Where(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == ActiveId)
        .Select(t => t.Value).SingleOrDefaultAsync(ct);

    private async Task BasariliDavetiKaydetAsync(Guid userId, string id)
    {
        await using var owned = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return;
        await database.Entry(user).ReloadAsync();
        if (!await UygunMuAsync(user, CancellationToken.None) || await AktifIdOkuAsync(userId, CancellationToken.None) != id) return;
        if (!(await users.SetAuthenticationTokenAsync(user, Provider, DeliveredId, id)).Succeeded)
            throw new InvalidOperationException("Başarılı davet durumu kaydedilemedi.");
        if (owned is not null) await owned.CommitAsync();
    }

    private async Task BasarisizDavetiGeriAlAsync(Guid userId, string? newId)
    {
        if (userId == Guid.Empty || newId is null) return;
        try
        {
            await using var owned = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            var user = await users.FindByIdAsync(userId.ToString());
            if (user is null) return;
            await database.Entry(user).ReloadAsync();
            // Başka bir yeniden gönderim veya başarılı parola işlemini geri alma.
            if (!await UygunMuAsync(user, CancellationToken.None) || await AktifIdOkuAsync(userId, CancellationToken.None) != newId) return;
            // Pending bir yeniden gönderimi değil, yalnız SMTP kabulü kaydedilmiş son daveti geri getir.
            var previousId = await database.UserTokens.AsNoTracking()
                .Where(t => t.UserId == userId && t.LoginProvider == Provider && t.Name == DeliveredId)
                .Select(t => t.Value).SingleOrDefaultAsync();
            var restored = previousId is null
                ? await users.RemoveAuthenticationTokenAsync(user, Provider, ActiveId)
                : await users.SetAuthenticationTokenAsync(user, Provider, ActiveId, previousId);
            if (restored.Succeeded && owned is not null) await owned.CommitAsync();
            if (!restored.Succeeded) logger.LogWarning("Başarısız ilk davet geri alınamadı; yeniden gönderim gerekir.");
        }
        catch (Exception) { logger.LogWarning("Başarısız ilk davet geri alınamadı; yeniden gönderim gerekir."); }
    }

    public async Task<SifreSifirlamaSonucu> OlusturAsync(IlkParolaModel model, CancellationToken ct = default)
    {
        if (!Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true))
            return SifreSifirlamaSonucu.ParolaKosullariUygunDegil;
        if (database.ChangeTracker.HasChanges() || database.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            return SifreSifirlamaSonucu.BaglantiGecersiz;
        await using var owned = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var transaction = owned ?? database.Database.CurrentTransaction!;
        var savepoint = Guid.NewGuid().ToString("N");
        if (owned is null) await transaction.CreateSavepointAsync(savepoint, ct);
        var completed = false; UygulamaKullanicisi? user = null; DegisiklikGecmisi? audit = null;
        try
        {
            user = await users.FindByIdAsync(model.UserId.ToString());
            if (user is not null) await database.Entry(user).ReloadAsync(ct);
            if (user is null || database.Entry(user).State == EntityState.Detached ||
                !await GecerliMiAsync(model.UserId, model.DavetId, model.Token, ct))
                return SifreSifirlamaSonucu.BaglantiGecersiz;
            var result = await users.AddPasswordAsync(user, model.YeniParola);
            if (!result.Succeeded)
                return result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                    ? SifreSifirlamaSonucu.ParolaKosullariUygunDegil : SifreSifirlamaSonucu.BaglantiGecersiz;
            user.EmailConfirmed = true;
            if (!(await users.UpdateAsync(user)).Succeeded ||
                !(await users.RemoveAuthenticationTokenAsync(user, Provider, ActiveId)).Succeeded ||
                !(await users.RemoveAuthenticationTokenAsync(user, Provider, DeliveredId)).Succeeded)
                return SifreSifirlamaSonucu.BaglantiGecersiz;
            audit = new() { Id = Guid.NewGuid(), VarlikTuru = nameof(UygulamaKullanicisi), VarlikAnahtari = user.Id.ToString(),
                Islem = "IlkParolaOlustur", YapanKullaniciId = user.Id, IslemAni = DateTimeOffset.UtcNow,
                OncekiDeger = "{\"ParolaHazir\":false,\"KurumsalAdresDogrulandi\":false}",
                YeniDeger = "{\"ParolaHazir\":true,\"KurumsalAdresDogrulandi\":true}" };
            database.DegisiklikGecmisi.Add(audit); await database.SaveChangesAsync(ct);
            if (owned is not null) await owned.CommitAsync(ct);
            completed = true;
            return SifreSifirlamaSonucu.Basarili;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("İlk parola oluşturma işlemi tamamlanamadı.");
            return SifreSifirlamaSonucu.BaglantiGecersiz;
        }
        finally
        {
            if (!completed)
            {
                if (owned is null) await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                else { await transaction.RollbackAsync(CancellationToken.None); await owned.DisposeAsync(); }
                if (audit is not null) database.Entry(audit).State = EntityState.Detached;
                foreach (var token in database.ChangeTracker.Entries<IdentityUserToken<Guid>>().Where(x => x.Entity.UserId == model.UserId && x.Entity.LoginProvider == Provider).ToArray())
                    token.State = EntityState.Detached;
                if (user is not null && database.Entry(user).State != EntityState.Detached) await database.Entry(user).ReloadAsync(CancellationToken.None);
            }
        }
    }

    private async Task<bool> UygunMuAsync(UygulamaKullanicisi user, CancellationToken ct)
    {
        if (user.EmailConfirmed || await users.HasPasswordAsync(user) ||
            !new EmailAddressAttribute().IsValid(user.Email) || string.IsNullOrWhiteSpace(user.Email) ||
            user.OgrenciNo is not { Length: 9 } || !user.OgrenciNo.All(c => c is >= '0' and <= '9') || user.UserName != user.OgrenciNo)
            return false;
        var roles = await users.GetRolesAsync(user);
        return roles.Count == 1 && roles[0] == UygulamaRolleri.Ogrenci && await database.OgrenciProgramlari.AnyAsync(
            p => p.OgrenciId == user.Id && p.AktifMi && p.HedefBolum == "Bilgisayar Mühendisliği", ct);
    }

    private static bool IdGecerli(string? id) => id is { Length: 32 } && id.All(Uri.IsHexDigit);
    private static bool Decode(string? value, out string decoded)
    {
        decoded = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) return false;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); return !string.IsNullOrWhiteSpace(decoded); }
        catch (FormatException) { return false; }
    }
}
