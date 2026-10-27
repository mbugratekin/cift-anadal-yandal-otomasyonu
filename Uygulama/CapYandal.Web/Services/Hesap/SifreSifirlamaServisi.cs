using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
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

public sealed class SifreSifirlamaServisi(
    UserManager<UygulamaKullanicisi> users,
    AppDbContext database,
    IEpostaGonderici epostaGonderici,
    IOptions<EpostaGonderimOptions> epostaOptions,
    ILogger<SifreSifirlamaServisi> logger) : ISifreSifirlamaServisi
{
    private const string ResetSaglayicisi = "CapYandal";
    private const string AktifResetAdi = "AktifSifreSifirlamaId";
    private const string SonBasariliResetAdi = "SonBasariliSifreSifirlamaId";
    private const string ResetAmaci = "ResetPassword";

    public async Task TalepEtAsync(string? kullaniciAdi, CancellationToken cancellationToken = default)
    {
        if (!epostaOptions.Value.Etkin || !IsOgrenciNo(kullaniciAdi) ||
            !Uri.TryCreate(epostaOptions.Value.UygulamaAdresi, UriKind.Absolute, out var appUri))
            return;

        var user = await users.FindByNameAsync(kullaniciAdi!);
        if (user is null || !await OgrenciKurtarmayaUygunMuAsync(user))
            return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var resetId = Guid.NewGuid().ToString("N");
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUri = new Uri(appUri.AbsoluteUri.TrimEnd('/') + "/Hesap/SifreSifirla");
        var link = QueryHelpers.AddQueryString(resetUri.ToString(), new Dictionary<string, string?>
        {
            [nameof(SifreSifirlamaModel.UserId)] = user.Id.ToString(),
            [nameof(SifreSifirlamaModel.ResetId)] = resetId,
            [nameof(SifreSifirlamaModel.Token)] = encodedToken
        });
        try
        {
            await using var transaction = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var currentUser = await database.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={user.Id}").SingleOrDefaultAsync(cancellationToken);
            if (currentUser is not null) await database.Entry(currentUser).ReloadAsync(cancellationToken);
            if (currentUser is null ||
                database.Entry(currentUser).State == EntityState.Detached ||
                !await OgrenciKurtarmayaUygunMuAsync(currentUser) ||
                !await users.VerifyUserTokenAsync(currentUser,
                    users.Options.Tokens.PasswordResetTokenProvider, ResetAmaci, token))
                return;

            if (!await KurtarmaTalepSiniri.AyirAsync(database,users,currentUser,DateTimeOffset.UtcNow,cancellationToken)) return;
            var result = await users.SetAuthenticationTokenAsync(
                currentUser, ResetSaglayicisi, AktifResetAdi, resetId);
            if (!result.Succeeded)
            {
                logger.LogWarning("Parola sıfırlama bağlantısı etkinleştirilemedi.");
                return;
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("Parola sıfırlama bağlantısı kaydedilemedi.");
            return;
        }

        var message = HesapEpostaMetinleri.ParolaSifirlama(user.Email!, link);

        try
        {
            var delivery = await epostaGonderici.GonderAsync(message, cancellationToken);
            if (delivery == EpostaGonderimSonucu.SmtpSunucusuKabulEtti)
            {
                await BasariliSifirlamayiKaydetAsync(user.Id, resetId);
                return;
            }
            await BasarisizSifirlamayiGeriAlAsync(user.Id, resetId);
            logger.LogInformation("Parola sıfırlama e-postası gönderim katmanı tarafından devre dışı bırakıldı.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await BasarisizSifirlamayiGeriAlAsync(user.Id, resetId);
            throw;
        }
        catch (Exception)
        {
            await BasarisizSifirlamayiGeriAlAsync(user.Id, resetId);
            logger.LogWarning("Parola sıfırlama bağlantısı kaydedilemedi.");
        }
    }

    public async Task<bool> BaglantiGecerliMiAsync(
        Guid userId,
        string? resetId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !IsResetId(resetId) || !TryDecodeToken(token, out var decodedToken))
            return false;

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await OgrenciKurtarmayaUygunMuAsync(user))
            return false;

        var activeResetId = await users.GetAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi);
        return SameResetId(resetId!, activeResetId) &&
            await users.VerifyUserTokenAsync(user,
                users.Options.Tokens.PasswordResetTokenProvider, ResetAmaci, decodedToken);
    }

    public async Task<SifreSifirlamaSonucu> SifirlaAsync(
        SifreSifirlamaModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.UserId == Guid.Empty || !IsResetId(model.ResetId) || !TryDecodeToken(model.Token, out var token))
            return SifreSifirlamaSonucu.BaglantiGecersiz;
        if (model.YeniParola is not { Length: >= 8 and <= 20 } || model.YeniParola != model.YeniParolaTekrar)
            return SifreSifirlamaSonucu.ParolaKosullariUygunDegil;
        if (database.ChangeTracker.HasChanges() || database.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            return SifreSifirlamaSonucu.BaglantiGecersiz;
        await using var owned = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var transaction = owned ?? database.Database.CurrentTransaction!;
        var savepoint = Guid.NewGuid().ToString("N");
        if (owned is null) await transaction.CreateSavepointAsync(savepoint, cancellationToken);
        var completed = false; UygulamaKullanicisi? user = null; DegisiklikGecmisi? audit = null;
        try
        {
            user = await users.FindByIdAsync(model.UserId.ToString());
            if (user is not null) await database.Entry(user).ReloadAsync(cancellationToken);
            if (user is null || database.Entry(user).State == EntityState.Detached || !await OgrenciKurtarmayaUygunMuAsync(user))
                return SifreSifirlamaSonucu.BaglantiGecersiz;

            var activeResetId = await users.GetAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi);
            if (!SameResetId(model.ResetId, activeResetId) ||
                !await users.VerifyUserTokenAsync(user,
                    users.Options.Tokens.PasswordResetTokenProvider, ResetAmaci, token))
                return SifreSifirlamaSonucu.BaglantiGecersiz;

            var before = JsonSerializer.Serialize(new { ParolaHazir = await users.HasPasswordAsync(user), user.ParolaDegisikligiZorunlu,
                GeciciParolaVar = user.GeciciParolaBitisAni.HasValue });
            var resetResult = await users.ResetPasswordAsync(user, token, model.YeniParola);
            if (!resetResult.Succeeded)
            {
                var passwordRejected = resetResult.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal));
                return passwordRejected
                    ? SifreSifirlamaSonucu.ParolaKosullariUygunDegil
                    : SifreSifirlamaSonucu.BaglantiGecersiz;
            }

            user.ParolaDegisikligiZorunlu = false;
            user.GeciciParolaBitisAni = null;
            var updateResult = await users.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return SifreSifirlamaSonucu.BaglantiGecersiz;

            var removeTokenResult = await users.RemoveAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi);
            var removeDeliveredResult = await users.RemoveAuthenticationTokenAsync(user, ResetSaglayicisi, SonBasariliResetAdi);
            if (!removeTokenResult.Succeeded || !removeDeliveredResult.Succeeded)
                return SifreSifirlamaSonucu.BaglantiGecersiz;

            audit = new() { Id = Guid.NewGuid(), VarlikTuru = nameof(UygulamaKullanicisi), VarlikAnahtari = user.Id.ToString(),
                Islem = "ParolaSifirla", YapanKullaniciId = user.Id, IslemAni = DateTimeOffset.UtcNow, OncekiDeger = before,
                YeniDeger = JsonSerializer.Serialize(new { ParolaHazir = true, Sifirlandi = true, user.ParolaDegisikligiZorunlu, GeciciParolaVar = false }) };
            database.DegisiklikGecmisi.Add(audit); await database.SaveChangesAsync(cancellationToken);
            if (owned is not null) await transaction.CommitAsync(cancellationToken);
            completed = true;
            // Dış işlemi çağıran henüz commit etmediyse başarı bildirimi gönderilmez.
            if (owned is not null) await SifirlamaBildirimiGonderAsync(user.Email!, cancellationToken);
            return SifreSifirlamaSonucu.Basarili;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("Parola sıfırlama işlemi tamamlanamadı.");
            return SifreSifirlamaSonucu.BaglantiGecersiz;
        }
        finally
        {
            if (!completed)
            {
                if (owned is null) await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                else { await transaction.RollbackAsync(CancellationToken.None); await owned.DisposeAsync(); }
                if (audit is not null) database.Entry(audit).State = EntityState.Detached;
                foreach (var storedToken in database.ChangeTracker.Entries<IdentityUserToken<Guid>>().Where(x => x.Entity.UserId == model.UserId && x.Entity.LoginProvider == ResetSaglayicisi).ToArray())
                    storedToken.State = EntityState.Detached;
                if (user is not null && database.Entry(user).State != EntityState.Detached) await database.Entry(user).ReloadAsync(CancellationToken.None);
            }
        }
    }

    private async Task BasariliSifirlamayiKaydetAsync(Guid userId, string resetId)
    {
        await using var owned = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return;
        await database.Entry(user).ReloadAsync();
        if (!await OgrenciKurtarmayaUygunMuAsync(user) ||
            await users.GetAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi) != resetId) return;
        if (!(await users.SetAuthenticationTokenAsync(user, ResetSaglayicisi, SonBasariliResetAdi, resetId)).Succeeded)
            throw new InvalidOperationException("Başarılı parola sıfırlama durumu kaydedilemedi.");
        if (owned is not null) await owned.CommitAsync();
    }

    private async Task BasarisizSifirlamayiGeriAlAsync(Guid userId, string? resetId)
    {
        if (userId == Guid.Empty || resetId is null) return;
        try
        {
            await using var owned = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            var user = await users.FindByIdAsync(userId.ToString());
            if (user is null) return;
            await database.Entry(user).ReloadAsync();
            if (!await OgrenciKurtarmayaUygunMuAsync(user) ||
                await users.GetAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi) != resetId) return;
            var previousId = await users.GetAuthenticationTokenAsync(user, ResetSaglayicisi, SonBasariliResetAdi);
            var restored = previousId is null
                ? await users.RemoveAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi)
                : await users.SetAuthenticationTokenAsync(user, ResetSaglayicisi, AktifResetAdi, previousId);
            if (restored.Succeeded && owned is not null) await owned.CommitAsync();
            if (!restored.Succeeded) logger.LogWarning("Başarısız parola sıfırlama talebi geri alınamadı.");
        }
        catch (Exception) { logger.LogWarning("Başarısız parola sıfırlama talebi geri alınamadı."); }
    }

    private async Task SifirlamaBildirimiGonderAsync(string address, CancellationToken cancellationToken)
    {
        // Şifre kaydedildi; bildirim hatası başarılı işlemi geri alamaz.
        try
        {
            var result = await epostaGonderici.GonderAsync(HesapEpostaMetinleri.ParolaSifirlandi(address), cancellationToken);
            if (result != EpostaGonderimSonucu.SmtpSunucusuKabulEtti)
                logger.LogWarning("Şifre sıfırlama bildirimi gönderilemedi.");
        }
        catch (Exception)
        {
            logger.LogWarning("Şifre sıfırlama bildirimi gönderilemedi.");
        }
    }

    private async Task<bool> OgrenciKurtarmayaUygunMuAsync(UygulamaKullanicisi user)
    {
        if (!user.EmailConfirmed ||
            string.IsNullOrWhiteSpace(user.Email) || !IsEmail(user.Email) ||
            !IsOgrenciNo(user.OgrenciNo) || !string.Equals(user.UserName, user.OgrenciNo, StringComparison.Ordinal))
            return false;

        var roles = await users.GetRolesAsync(user);
        if (roles.Count != 1 || roles[0] != UygulamaRolleri.Ogrenci)
            return false;

        return await database.OgrenciProgramlari.AnyAsync(
            program => program.OgrenciId == user.Id && program.AktifMi && program.HedefBolum == "Bilgisayar Mühendisliği");
    }

    private static bool IsOgrenciNo(string? value) =>
        value is { Length: 9 } && value.All(character => character is >= '0' and <= '9');

    private static bool IsResetId(string? value) =>
        value is { Length: 32 } && value.All(Uri.IsHexDigit);

    private static bool SameResetId(string value, string? activeValue)
    {
        if (!IsResetId(value) || !IsResetId(activeValue))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(value), Encoding.ASCII.GetBytes(activeValue!));
    }

    private static bool TryDecodeToken(string? value, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
            return false;

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value));
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsEmail(string value)
    {
        try { return new MailAddress(value).Address == value; }
        catch (FormatException) { return false; }
    }
}
