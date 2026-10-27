using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Ogrenci;

public enum OgrenciKayitSonucu { Basarili, Gecersiz, IzinYok, Cakisma, Basarisiz }

public sealed class OgrenciKayitServisi(UserManager<UygulamaKullanicisi> users,
    AppDbContext database, HesapErisimi access, ILogger<OgrenciKayitServisi> logger)
{
    public async Task<OgrenciKayitSonucu> EkleAsync(Guid actorId, OgrenciKayitModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true))
            return OgrenciKayitSonucu.Gecersiz;
        try
        {
            if (database.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return OgrenciKayitSonucu.Basarisiz;
            await using var ownedTransaction = database.Database.CurrentTransaction is null
                ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
            var transaction = ownedTransaction ?? database.Database.CurrentTransaction!;
            var savepoint = Guid.NewGuid().ToString("N");
            if (ownedTransaction is null) await transaction.CreateSavepointAsync(savepoint, cancellationToken);
            var completed = false;
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return OgrenciKayitSonucu.IzinYok;
                await database.Entry(actor).ReloadAsync(cancellationToken);
                var permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                    return OgrenciKayitSonucu.IzinYok;

                if (!await database.KaynakAnaDallar.AnyAsync(x => x.Tur == model.Tur && x.Ad == model.KaynakAnaDal && x.EtkinMi, cancellationToken))
                    return OgrenciKayitSonucu.Gecersiz;
                var studentEmail = model.OgrenciNo + "@ogr.selcuk.edu.tr";
                if (await users.FindByNameAsync(model.OgrenciNo) is not null ||
                    await users.FindByEmailAsync(studentEmail) is not null ||
                    await database.Users.AnyAsync(user => user.OgrenciNo == model.OgrenciNo, cancellationToken))
                    return OgrenciKayitSonucu.Cakisma;

                // MÜFREDAT ATAMA: Kabul yılını aşmayan en yeni müfredat sürümü otomatik seçilir.
                // Tarayıcıdan gönderilebilecek bir şablon kimliği bu kararı değiştiremez.
                var curriculumVersion = await database.Mufredatlar.AsNoTracking()
                    .Where(version => version.BaslangicYili <= model.KabulAkademikYilBaslangici)
                    .OrderByDescending(version => version.BaslangicYili)
                    .FirstOrDefaultAsync(cancellationToken);
                if (curriculumVersion is null || !curriculumVersion.EtkinMi) return OgrenciKayitSonucu.Gecersiz;
                var curriculum = await (
                    from template in database.DersSablonlari.AsNoTracking()
                    where template.MufredatId == curriculumVersion.Id && template.EtkinMi &&
                          template.Tur == model.Tur && template.HedefBolum == "Bilgisayar Mühendisliği" &&
                          ((template.Tur == OgrenciProgramTuru.YanDal && template.KaynakAnaDal == null) ||
                           (template.Tur == OgrenciProgramTuru.Cap && template.KaynakAnaDal == model.KaynakAnaDal)) &&
                          database.SablonDersleri.Any(course => course.SablonId == template.Id && course.EtkinMi)
                    select template).SingleOrDefaultAsync(cancellationToken);
                if (curriculum is null) return OgrenciKayitSonucu.Gecersiz;

                var student = new UygulamaKullanicisi
                {
                    Id = Guid.NewGuid(), UserName = model.OgrenciNo, OgrenciNo = model.OgrenciNo,
                    Ad = model.Ad, Soyad = model.Soyad, Email = studentEmail,
                    EmailConfirmed = false, AktifMi = true, LockoutEnabled = true
                };
                var created = await users.CreateAsync(student);
                if (!created.Succeeded)
                    return created.Errors.Any(error => error.Code is "DuplicateUserName" or "DuplicateEmail")
                        ? OgrenciKayitSonucu.Cakisma : OgrenciKayitSonucu.Basarisiz;
                if (!(await users.AddToRoleAsync(student, UygulamaRolleri.Ogrenci)).Succeeded)
                    return OgrenciKayitSonucu.Basarisiz;

                var now = DateTimeOffset.UtcNow;
                // Program sıralama tarihi kullanıcıdan ikinci kez istenmez; kabul döneminden türetilir.
                var programBaslangici = model.KabulYariyili == AkademikYariyil.Guz
                    ? new DateOnly(model.KabulAkademikYilBaslangici!.Value, 9, 1)
                    : new DateOnly(model.KabulAkademikYilBaslangici!.Value + 1, 2, 1);
                var program = new OgrenciProgrami
                {
                    KabulAkademikYilBaslangici = model.KabulAkademikYilBaslangici,
                    KabulYariyili = model.KabulYariyili,
                    Id = Guid.NewGuid(), OgrenciId = student.Id, Tur = model.Tur!.Value, MufredatSablonId = curriculum.Id,
                    HedefBolum = "Bilgisayar Mühendisliği", KaynakAnaDal = model.KaynakAnaDal,
                    BaslangicTarihi = programBaslangici, AktifMi = true, DegisiklikAni = now
                };
                database.OgrenciProgramlari.Add(program);
                database.DegisiklikGecmisi.Add(new DegisiklikGecmisi
                {
                    Id = Guid.NewGuid(), VarlikTuru = nameof(OgrenciProgrami), VarlikAnahtari = program.Id.ToString(),
                    Islem = "OgrenciKaydi", YapanKullaniciId = actorId, IslemAni = now,
                    YeniDeger = JsonSerializer.Serialize(new { program.OgrenciId, program.Tur, program.KaynakAnaDal, program.BaslangicTarihi, program.HedefBolum, program.MufredatSablonId, program.KabulAkademikYilBaslangici, program.KabulYariyili,
                        Kullanici = new { student.Id, student.OgrenciNo, student.Ad, student.Soyad, student.Email, student.EmailConfirmed, student.AktifMi } })
                });
                await database.SaveChangesAsync(cancellationToken);
                if (ownedTransaction is not null) await transaction.CommitAsync(cancellationToken);
                completed = true;
                return OgrenciKayitSonucu.Basarili;
            }
            finally
            {
                if (!completed)
                {
                    // İşlem başarısız olursa ara hesap kayıtları geri alınır.
                    if (ownedTransaction is null) await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
                    else await transaction.RollbackAsync(CancellationToken.None);
                    database.ChangeTracker.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return OgrenciKayitSonucu.Cakisma;
        }
        catch (Exception)
        {
            logger.LogWarning("Öğrenci kaydı tamamlanamadı.");
            return OgrenciKayitSonucu.Basarisiz;
        }
    }
}
