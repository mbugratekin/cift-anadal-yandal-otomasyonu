using System.ComponentModel.DataAnnotations;
using System.Data;
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

public enum PersonelHesapSonucu { Hazirlandi, Gonderildi, DevreDisi, UygunDegil, IzinYok, Cakisma, Basarisiz, ParolaKosullari }
public sealed record PersonelHazirlamaSonucu(PersonelHesapSonucu Durum, Guid? Id = null);

public static class PersonelErisimKurali
{
    internal const string Provider="CapYandal", PendingRole="PersonelHazirlananGorev";
    public static Task<string?> HazirlananGorevAsync(AppDbContext db,Guid id,CancellationToken ct=default) => db.UserTokens.AsNoTracking()
        .Where(t=>t.UserId==id && t.LoginProvider==Provider && t.Name==PendingRole &&
            (t.Value==UygulamaRolleri.Koordinator || t.Value==UygulamaRolleri.BolumBaskani)).Select(t=>t.Value).SingleOrDefaultAsync(ct);
}

// Personel hesabı hazırlığı — koordinatör hesabı açar, davet doğrulaması tek başına rol vermez.
public sealed class PersonelErisimServisi(AppDbContext db,UserManager<UygulamaKullanicisi> users,HesapErisimi access,
    IEpostaGonderici sender,IOptions<EpostaGonderimOptions> options,TimeProvider clock,ILogger<PersonelErisimServisi> logger)
{
    private const string RequestKey="AktifPersonelParolaId";
    private static string Purpose(UygulamaKullanicisi user,string id)=>$"PersonelParola:{id}:{user.Email}";

    public async Task<PersonelHazirlamaSonucu> HazirlaAsync(Guid actorId,PersonelHesapModeli model,CancellationToken ct=default)
    {
        if(!Validator.TryValidateObject(model,new(model),new List<ValidationResult>(),true) ||
            model.Rol is not (UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani) ||
            string.IsNullOrWhiteSpace(model.Ad) || string.IsNullOrWhiteSpace(model.Soyad) ||
            string.IsNullOrWhiteSpace(model.KullaniciAdi))
            return new(PersonelHesapSonucu.UygunDegil);
        var id=Guid.NewGuid();
        var result=await IslemAsync(async()=> {
            var actor=await KilitleAsync(actorId,ct);
            if(actor is null) return PersonelHesapSonucu.IzinYok;
            var permission=await access.OkuAsync(actor);
            if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator]))
                return PersonelHesapSonucu.IzinYok;
            var name=model.KullaniciAdi.Trim(); var address=model.KurumsalEposta.Trim();
            if(await db.Users.AnyAsync(u=>u.NormalizedUserName==users.NormalizeName(name) || u.NormalizedEmail==users.NormalizeEmail(address),ct))
                return PersonelHesapSonucu.Cakisma;
            var user=new UygulamaKullanicisi { Id=id,UserName=name,Email=address,Ad=model.Ad.Trim(),Soyad=model.Soyad.Trim(),
                AktifMi=false,EmailConfirmed=false };
            if(!(await users.CreateAsync(user)).Succeeded ||
                !(await users.SetAuthenticationTokenAsync(user,PersonelErisimKurali.Provider,PersonelErisimKurali.PendingRole,model.Rol)).Succeeded)
                return PersonelHesapSonucu.Cakisma;
            db.DegisiklikGecmisi.Add(new() { Id=Guid.NewGuid(),VarlikTuru="PersonelHazirligi",VarlikAnahtari=id.ToString(),
                Islem="HesapHazirla",YapanKullaniciId=actorId,IslemAni=clock.GetUtcNow(),OncekiDeger="null",
                YeniDeger=JsonSerializer.Serialize(new { KullaniciId=id,HazirlananGorev=model.Rol,AktifMi=false,
                    user.UserName,user.Ad,user.Soyad,user.Email,user.EmailConfirmed }) });
            await db.SaveChangesAsync(ct); return PersonelHesapSonucu.Hazirlandi;
        },ct);
        return new(result,result==PersonelHesapSonucu.Hazirlandi?id:null);
    }

    public Task<PersonelHesapSonucu> TalepEtAsync(string? name,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length>256) return Task.FromResult(PersonelHesapSonucu.UygunDegil);
        if(!options.Value.Etkin || !Uri.TryCreate(options.Value.UygulamaAdresi,UriKind.Absolute,out var app) || app.Scheme!="https")
            return Task.FromResult(PersonelHesapSonucu.DevreDisi);
        return IslemAsync(async()=> {
            var found=await users.FindByNameAsync(name.Trim());
            var user=found is null?null:await KilitleAsync(found.Id,ct);
            if(user is null || !await UygunMuAsync(user,ct)) return PersonelHesapSonucu.UygunDegil;
            if(!await KurtarmaTalepSiniri.AyirAsync(db,users,user,clock.GetUtcNow(),ct)) return PersonelHesapSonucu.UygunDegil;
            return await BaglantiGonderAsync(user,app,ct);
        },ct);
    }

    // Davet gönderilmeden önce görevli ve hazırlık hesabı yeniden kontrol edilir.
    public Task<PersonelHesapSonucu> DavetEtAsync(Guid actorId,Guid targetId,CancellationToken ct=default)
    {
        if(actorId==Guid.Empty || targetId==Guid.Empty || actorId==targetId)
            return Task.FromResult(PersonelHesapSonucu.UygunDegil);
        return IslemAsync(async()=> {
            var locked=new Dictionary<Guid,UygulamaKullanicisi>();
            foreach(var id in new[] {actorId,targetId}.Order()) {
                var user=await KilitleAsync(id,ct);
                if(user is null) return PersonelHesapSonucu.UygunDegil;
                locked.Add(id,user);
            }
            var permission=await access.OkuAsync(locked[actorId]);
            if(!permission.Izinli || permission.ParolaDegisikligiGerekli ||
                !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return PersonelHesapSonucu.IzinYok;
            var target=locked[targetId];
            if(target.OgrenciNo is not null || target.AktifMi || (await users.GetRolesAsync(target)).Count!=0 ||
                await PersonelErisimKurali.HazirlananGorevAsync(db,targetId,ct) is null ||
                string.IsNullOrWhiteSpace(target.Email) || !new EmailAddressAttribute().IsValid(target.Email))
                return PersonelHesapSonucu.UygunDegil;
            if(!options.Value.Etkin || !Uri.TryCreate(options.Value.UygulamaAdresi,UriKind.Absolute,out var app) || app.Scheme!="https")
                return PersonelHesapSonucu.DevreDisi;
            return await BaglantiGonderAsync(target,app,ct,actorId);
        },ct);
    }

    private async Task<PersonelHesapSonucu> BaglantiGonderAsync(UygulamaKullanicisi user,Uri app,CancellationToken ct,Guid? actorId=null)
    {
        var oncekiTalep=await db.UserTokens.AsNoTracking().AnyAsync(t=>t.UserId==user.Id &&
            t.LoginProvider==PersonelErisimKurali.Provider && t.Name==RequestKey,ct);
        var id=Guid.NewGuid().ToString("N");
        var token=await users.GenerateUserTokenAsync(user,TokenOptions.DefaultProvider,Purpose(user,id));
        if(!(await users.SetAuthenticationTokenAsync(user,PersonelErisimKurali.Provider,RequestKey,id)).Succeeded)
            return PersonelHesapSonucu.Basarisiz;
        // İşlem geçmişi yazılamazsa davet bağlantısı gönderilmez.
        if(actorId is { } actor) {
            db.DegisiklikGecmisi.Add(new() { Id=Guid.NewGuid(),VarlikTuru="PersonelErisimi",VarlikAnahtari=user.Id.ToString(),
                Islem="PersonelDavetTalebi",YapanKullaniciId=actor,IslemAni=clock.GetUtcNow(),
                OncekiDeger=JsonSerializer.Serialize(new { BekleyenDavet=oncekiTalep }),
                YeniDeger="{\"BekleyenDavet\":true}" });
            await db.SaveChangesAsync(ct);
        }
        var link=QueryHelpers.AddQueryString(app.AbsoluteUri.TrimEnd('/')+"/PersonelErisim/Parola",new Dictionary<string,string?> {
            ["UserId"]=user.Id.ToString(),["RequestId"]=id,["Token"]=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)) });
        return await sender.GonderAsync(new(user.Email!,"ÇAP / Yan Dal personel erişim doğrulama",
            $"İsteği doğrulamak için bağlantıyı açıp onaylayın:\n\n{link}\n\nBağlantı 24 saat ve tek kullanımlıktır. " +
            "Açılan sayfada parolanızı kendiniz belirleyin. Parolanız e-postayla gönderilmez. İsteği siz başlatmadıysanız kullanmayın."),ct)
            ==EpostaGonderimSonucu.SmtpSunucusuKabulEtti?PersonelHesapSonucu.Gonderildi:PersonelHesapSonucu.DevreDisi;
    }

    public async Task<bool> GecerliMiAsync(PersonelParolaOnayModeli model,CancellationToken ct=default)
    {
        if(model.UserId==Guid.Empty || model.RequestId is not { Length:32 } || !model.RequestId.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(model.Token) || model.Token.Length>4096) return false;
        string decoded;
        try { decoded=Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token)); } catch(FormatException) { return false; }
        var user=await users.FindByIdAsync(model.UserId.ToString());
        if(user is null) return false;
        await db.Entry(user).ReloadAsync(ct);
        if(db.Entry(user).State==EntityState.Detached || !await UygunMuAsync(user,ct)) return false;
        var active=await db.UserTokens.AsNoTracking().Where(t=>t.UserId==user.Id && t.LoginProvider==PersonelErisimKurali.Provider && t.Name==RequestKey)
            .Select(t=>t.Value).SingleOrDefaultAsync(ct);
        return active==model.RequestId && await users.VerifyUserTokenAsync(user,TokenOptions.DefaultProvider,Purpose(user,model.RequestId),decoded);
    }

    public Task<PersonelHesapSonucu> OnaylaAsync(PersonelParolaOnayModeli model,CancellationToken ct=default)
    {
        if (!Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true))
            return Task.FromResult(PersonelHesapSonucu.ParolaKosullari);
        return IslemAsync(async()=> {
            var user=await KilitleAsync(model.UserId,ct);
            if(user is null || !await GecerliMiAsync(model,ct)) return PersonelHesapSonucu.UygunDegil;
            var before=JsonSerializer.Serialize(new { ParolaHazir=await users.HasPasswordAsync(user), KurumsalAdresDogrulandi=user.EmailConfirmed,
                ZorunluParolaDegisimi=user.ParolaDegisikligiZorunlu, GeciciParolaVarMi=user.GeciciParolaBitisAni is not null });
            var password=model.YeniParola;
            foreach(var validator in users.PasswordValidators)
                if(!(await validator.ValidateAsync(users,user,password)).Succeeded) return PersonelHesapSonucu.ParolaKosullari;
            // Bağlantı e-posta sahipliğini kanıtlar; parola ikinci bir iletiye kopyalanmaz.
            user.EmailConfirmed=true; user.ParolaDegisikligiZorunlu=false; user.GeciciParolaBitisAni=null;
            var reset = await users.HasPasswordAsync(user)
                ? await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), password)
                : await users.AddPasswordAsync(user, password);
            if (!reset.Succeeded) return PersonelHesapSonucu.Basarisiz;
            if(!(await users.UpdateSecurityStampAsync(user)).Succeeded ||
                !(await users.RemoveAuthenticationTokenAsync(user,PersonelErisimKurali.Provider,RequestKey)).Succeeded)
                return PersonelHesapSonucu.Basarisiz;
            db.DegisiklikGecmisi.Add(new() { Id=Guid.NewGuid(),VarlikTuru="PersonelErisimi",VarlikAnahtari=user.Id.ToString(),
                Islem="PersonelParolaBelirle",YapanKullaniciId=user.Id,IslemAni=clock.GetUtcNow(),OncekiDeger=before,
                YeniDeger="{\"ParolaHazir\":true,\"KurumsalAdresDogrulandi\":true,\"ZorunluParolaDegisimi\":false,\"GeciciParolaVarMi\":false}" });
            await db.SaveChangesAsync(ct); return PersonelHesapSonucu.Gonderildi;
        },ct);
    }

    private async Task<bool> UygunMuAsync(UygulamaKullanicisi user,CancellationToken ct)
    {
        if(user.OgrenciNo is not null || string.IsNullOrWhiteSpace(user.Email) || !new EmailAddressAttribute().IsValid(user.Email)) return false;
        var roles=await users.GetRolesAsync(user);
        if(roles.Count==0 && !user.AktifMi && await PersonelErisimKurali.HazirlananGorevAsync(db,user.Id,ct) is not null) return true;
        return user.AktifMi && user.EmailConfirmed && roles.Count==1 &&
            roles[0] is UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani && await users.HasPasswordAsync(user);
    }
    private async Task<UygulamaKullanicisi?> KilitleAsync(Guid id,CancellationToken ct)
    {
        if(id==Guid.Empty) return null;
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
        if(user is not null) await db.Entry(user).ReloadAsync(ct);
        return user;
    }
    private async Task<PersonelHesapSonucu> IslemAsync(Func<Task<PersonelHesapSonucu>> work,CancellationToken ct)
    {
        if(db.ChangeTracker.HasChanges()) return PersonelHesapSonucu.Basarisiz;
        try {
            if(db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
                return PersonelHesapSonucu.Basarisiz;
            await using var own=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
            var tx=own??db.Database.CurrentTransaction!; var point=Guid.NewGuid().ToString("N");
            if(own is null) await tx.CreateSavepointAsync(point,ct);
            bool completed=false;
            try {
                var result=await work();
                if(result is PersonelHesapSonucu.Hazirlandi or PersonelHesapSonucu.Gonderildi) {
                    if(own is not null) await tx.CommitAsync(ct); completed=true;
                }
                return result;
            } finally {
                if(!completed) { if(own is null) await tx.RollbackToSavepointAsync(point,CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            }
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(DbUpdateException) { return PersonelHesapSonucu.Cakisma; }
        catch(Exception) { logger.LogWarning("Personel erişim işlemi tamamlanamadı; parola veya token günlüğe yazılmadı."); return PersonelHesapSonucu.Basarisiz; }
    }
}
