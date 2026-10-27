using System.Data;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Eposta;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CapYandal.Web.Services.Hesap;

public sealed class KisiselEpostaKurulumServisi(
    UserManager<UygulamaKullanicisi> users, AppDbContext database, HesapErisimi access,
    IEpostaGonderici sender, IOptions<EpostaGonderimOptions> options,
    ILogger<KisiselEpostaKurulumServisi> logger) : IKisiselEpostaKurulumServisi
{
    private const string Provider = "CapYandal";
    private const string RequestName = "KisiselEpostaKurulumId";

    public async Task<KisiselEpostaTalepSonucu> TalepEtAsync(
        Guid userId, string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || database.ChangeTracker.HasChanges()) return KisiselEpostaTalepSonucu.Basarisiz;
        address = address.Trim();
        if (!IsEmail(address)) return KisiselEpostaTalepSonucu.Basarisiz;
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await KurulumaUygunMuAsync(user)) return KisiselEpostaTalepSonucu.IzinYok;
        var appAddressValid = Uri.TryCreate(options.Value.UygulamaAdresi, UriKind.Absolute, out var appUri);
        var canSend = options.Value.Etkin && appAddressValid;

        string link;
        try
        {
            await using var transaction = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var tx = transaction ?? database.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (transaction is null) await tx.CreateSavepointAsync(point, cancellationToken);
            var initial = database.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
            var completed = false;
            try
            {
                await database.Entry(user).ReloadAsync(cancellationToken);
                if (database.Entry(user).State == EntityState.Detached || !await KurulumaUygunMuAsync(user))
                    return KisiselEpostaTalepSonucu.IzinYok;

                var requestId = Guid.NewGuid().ToString("N");
                var before = AdresKopyasi(user);
                user.BekleyenKisiselEposta = address;
                if (!(await users.UpdateAsync(user)).Succeeded)
                    return KisiselEpostaTalepSonucu.Basarisiz;

                if (!canSend)
                {
                    // Adres girişi erişim için yeterlidir; gönderilmemiş adres onaylanmış sayılmaz.
                    if (!(await users.RemoveAuthenticationTokenAsync(user, Provider, RequestName)).Succeeded)
                        return KisiselEpostaTalepSonucu.Basarisiz;
                    await DenetimYazAsync(user, "KisiselEpostaTalep", before, cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    completed = true;
                    return KisiselEpostaTalepSonucu.DevreDisi;
                }
                if (!(await users.SetAuthenticationTokenAsync(user, Provider, RequestName, requestId)).Succeeded)
                    return KisiselEpostaTalepSonucu.Basarisiz;

                var token = await users.GenerateUserTokenAsync(user, users.Options.Tokens.EmailConfirmationTokenProvider,
                    Purpose(requestId, address));
                link = QueryHelpers.AddQueryString(appUri!.AbsoluteUri.TrimEnd('/') + "/Hesap/KisiselEpostaDogrula",
                    new Dictionary<string, string?>
                    {
                        ["UserId"] = user.Id.ToString(),
                        ["RequestId"] = requestId,
                        ["Token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
                    });
                await DenetimYazAsync(user, "KisiselEpostaTalep", before, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                completed = true;
            }
            finally { if (!completed) await GeriAlAsync(tx, transaction is not null, point, initial, user); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Kişisel e-posta doğrulama isteği kaydedilemedi.");
            return KisiselEpostaTalepSonucu.Basarisiz;
        }

        try
        {
            var delivery = await sender.GonderAsync(HesapEpostaMetinleri.KisiselAdresDogrulama(address, link), cancellationToken);
            return delivery == EpostaGonderimSonucu.SmtpSunucusuKabulEtti
                ? KisiselEpostaTalepSonucu.Gonderildi : KisiselEpostaTalepSonucu.DevreDisi;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Kişisel e-posta doğrulama iletisi gönderilemedi.");
            return KisiselEpostaTalepSonucu.DevreDisi;
        }
    }

    public async Task<string?> BekleyenAdresiOkuAsync(
        Guid userId, string? requestId, string? token, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && await BaglantiGecerliMiAsync(user, requestId, token)
            ? user.BekleyenKisiselEposta : null;
    }

    public async Task<bool> DogrulaAsync(
        Guid userId, string? requestId, string? token, CancellationToken cancellationToken = default)
    {
        if (database.ChangeTracker.HasChanges()) return false;
        try
        {
            await using var transaction = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var tx = transaction ?? database.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (transaction is null) await tx.CreateSavepointAsync(point, cancellationToken);
            var initial = database.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
            var completed = false; UygulamaKullanicisi? user = null;
            try
            {
                user = await users.FindByIdAsync(userId.ToString());
                if (user is null) return false;
                await database.Entry(user).ReloadAsync(cancellationToken);
                if (database.Entry(user).State == EntityState.Detached ||
                    !await BaglantiGecerliMiAsync(user, requestId, token)) return false;

                var before = AdresKopyasi(user);
                user.KisiselEposta = user.BekleyenKisiselEposta;
                user.BekleyenKisiselEposta = null;
                user.KisiselEpostaOnayliMi = true;
                if (!(await users.UpdateAsync(user)).Succeeded ||
                    !(await users.RemoveAuthenticationTokenAsync(user, Provider, RequestName)).Succeeded ||
                    !(await users.UpdateSecurityStampAsync(user)).Succeeded) return false;

                await DenetimYazAsync(user, "KisiselEpostaDogrula", before, cancellationToken);

                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                completed = true;
                return true;
            }
            finally { if (!completed) await GeriAlAsync(tx, transaction is not null, point, initial, user); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            logger.LogWarning("Kişisel e-posta doğrulaması tamamlanamadı.");
            return false;
        }
    }

    private async Task<bool> KurulumaUygunMuAsync(UygulamaKullanicisi user)
    {
        var state = await access.OkuAsync(user);
        return state.Izinli &&
            state.Roller.Length == 1 && state.Roller[0] == UygulamaRolleri.Ogrenci;
    }

    private async Task<bool> BaglantiGecerliMiAsync(UygulamaKullanicisi user, string? requestId, string? token)
    {
        if (!Guid.TryParseExact(requestId, "N", out _) || string.IsNullOrWhiteSpace(token) || token.Length > 4096 ||
            !IsEmail(user.BekleyenKisiselEposta) || !await KurulumaUygunMuAsync(user)) return false;
        if (!string.Equals(requestId, await users.GetAuthenticationTokenAsync(user, Provider, RequestName), StringComparison.Ordinal))
            return false;
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return await users.VerifyUserTokenAsync(user, users.Options.Tokens.EmailConfirmationTokenProvider,
                Purpose(requestId!, user.BekleyenKisiselEposta!), decoded);
        }
        catch (FormatException) { return false; }
    }

    private static string Purpose(string requestId, string address) => $"KisiselEpostaKurulum:{requestId}:{address}";
    // Geçmişte yalnız adres ve onay durumu tutulur; bağlantı kopyalanmaz.
    private static object AdresKopyasi(UygulamaKullanicisi user) => new { user.KisiselEposta, user.BekleyenKisiselEposta, user.KisiselEpostaOnayliMi };
    private async Task DenetimYazAsync(UygulamaKullanicisi user, string operation, object before, CancellationToken ct)
    {
        database.DegisiklikGecmisi.Add(new()
        {
            Id = Guid.NewGuid(),
            VarlikTuru = nameof(UygulamaKullanicisi),
            VarlikAnahtari = user.Id.ToString(),
            Islem = operation,
            YapanKullaniciId = user.Id,
            IslemAni = DateTimeOffset.UtcNow,
            OncekiDeger = JsonSerializer.Serialize(before),
            YeniDeger = JsonSerializer.Serialize(AdresKopyasi(user))
        });
        await database.SaveChangesAsync(ct);
    }
    // İşlem başarısız olursa ara hesap değişiklikleri geri alınır.
    private async Task GeriAlAsync(IDbContextTransaction tx, bool owned, string point, HashSet<object> initial, UygulamaKullanicisi? user)
    {
        if (owned) await tx.RollbackAsync(CancellationToken.None); else await tx.RollbackToSavepointAsync(point, CancellationToken.None);
        foreach (var entry in database.ChangeTracker.Entries().Where(x => !initial.Contains(x.Entity) ||
            x.Entity is IdentityUserToken<Guid> token && token.UserId == user?.Id).ToArray()) entry.State = EntityState.Detached;
        if (user is not null && database.Entry(user).State != EntityState.Detached) await database.Entry(user).ReloadAsync(CancellationToken.None);
    }
    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return false;
        try { return new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }
}
