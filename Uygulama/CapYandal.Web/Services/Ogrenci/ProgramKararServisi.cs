using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Ogrenci;

public enum ProgramKararSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }

// Manuel karar — program, oturum iptali ve eski/yeni değer aynı Serializable işlemde yazılır.
public sealed class ProgramKararServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<ProgramKararServisi> logger)
{
    public async Task<ProgramKararSonucu> KaydetAsync(Guid actorId, ProgramKararModeli model, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (actorId == Guid.Empty || !Validator.TryValidateObject(model, new(model), new List<ValidationResult>(), true))
            return ProgramKararSonucu.Gecersiz;
        if (db.ChangeTracker.HasChanges()) return ProgramKararSonucu.Basarisiz;
        try
        {
            if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return ProgramKararSonucu.Basarisiz;
            var studentId = await db.OgrenciProgramlari.AsNoTracking().Where(p => p.Id == model.ProgramId &&
                p.HedefBolum == "Bilgisayar Mühendisliği").Select(p => (Guid?)p.OgrenciId).SingleOrDefaultAsync(ct);
            if (studentId is null) return ProgramKararSonucu.Bulunamadi;
            if (studentId == actorId) return ProgramKararSonucu.IzinYok;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                var locked = new Dictionary<Guid, UygulamaKullanicisi>();
                foreach (var id in new[] { actorId, studentId.Value }.Order())
                {
                    var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [Kullanicilar] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
                    if (user is null) return ProgramKararSonucu.Bulunamadi;
                    await db.Entry(user).ReloadAsync(ct);
                    locked.Add(id, user);
                }
                var permission = await access.OkuAsync(locked[actorId]);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.SequenceEqual([UygulamaRolleri.Koordinator]))
                    return ProgramKararSonucu.IzinYok;
                if (!(await users.GetRolesAsync(locked[studentId.Value])).SequenceEqual([UygulamaRolleri.Ogrenci]))
                    return ProgramKararSonucu.IzinYok;
                var program = await db.OgrenciProgramlari.FromSqlInterpolated(
                    $"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={model.ProgramId}").SingleOrDefaultAsync(ct);
                if (program is null) return ProgramKararSonucu.Bulunamadi;
                await db.Entry(program).ReloadAsync(ct);
                if (program.OgrenciId != studentId || program.HedefBolum != "Bilgisayar Mühendisliği") return ProgramKararSonucu.IzinYok;
                if (!program.RowVersion.SequenceEqual(model.Surum) || program.SonKarar == model.Karar) return ProgramKararSonucu.Cakisma;
                if (!ProgramKararKurali.UygunMu(program.Tur, model.Karar!.Value)) return ProgramKararSonucu.Gecersiz;
                var before = Deger(program);
                program.AktifMi = false;
                program.SonKarar = model.Karar;
                program.KararGerekcesi = model.Gerekce.Trim();
                program.KararVerenKullaniciId = actorId;
                program.KararAni = clock.GetUtcNow();
                program.DegisiklikAni = program.KararAni.Value;
                if (!(await users.UpdateSecurityStampAsync(locked[studentId.Value])).Succeeded) return ProgramKararSonucu.Cakisma;
                db.DegisiklikGecmisi.Add(new()
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(OgrenciProgrami), VarlikAnahtari = program.Id.ToString(),
                    Islem = "ManuelProgramKarari", YapanKullaniciId = actorId, IslemAni = program.KararAni.Value,
                    OncekiDeger = before, YeniDeger = Deger(program)
                });
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true;
                return ProgramKararSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                    db.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateException) { return ProgramKararSonucu.Cakisma; }
        catch (Exception error)
        {
            logger.LogWarning("Program kararı kaydedilemedi: {HataTuru}.", error.GetType().Name);
            return ProgramKararSonucu.Basarisiz;
        }
    }

    private static string Deger(OgrenciProgrami p) => JsonSerializer.Serialize(new
    { p.Id, p.AktifMi, p.SonKarar, p.KararGerekcesi, p.KararVerenKullaniciId, p.KararAni });
}
