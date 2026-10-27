using System.Data;
using System.Text.Json;
using CapYandal.Web.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.DersKaydi;

public sealed partial class GecmisDersAktarimServisi
{
    // Yalnız açık koordinatör POST'u dönem seçeneklerini hazırlar; GET kayıt oluşturmaz.
    // Not/listeler, kayıt kapıları ve işlem dönemi değişmez.
    public async Task<DersDenemeSonucu> DonemleriHazirlaAsync(Guid actorId, Guid programId, CancellationToken ct)
    {
        if (actorId == Guid.Empty || programId == Guid.Empty) return DersDenemeSonucu.Gecersiz;
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges()) return DersDenemeSonucu.Basarisiz;
        var initial = db.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        bool complete = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!await KoordinatorMuAsync(actorId, ct)) return DersDenemeSonucu.IzinYok;
                var current = await db.Donemler.FromSqlRaw("SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [IslemDonemiMi]=1").AsNoTracking().SingleOrDefaultAsync(ct);
                var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={programId}").AsNoTracking().SingleOrDefaultAsync(ct);
                if (program?.HedefBolum != "Bilgisayar Mühendisliği") return DersDenemeSonucu.Bulunamadi;
                if (current is null || program.KabulAkademikYilBaslangici is not { } start || start is < 2 or > 9998 ||
                    program.KabulYariyili is not (AkademikYariyil.Guz or AkademikYariyil.Bahar) ||
                    start > current.AkademikYilBaslangici || current.AkademikYilBaslangici - start > 100)
                    return DersDenemeSonucu.Gecersiz;
                var existing = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [AkademikYilBaslangici]>={start} AND [AkademikYilBaslangici]<={current.AkademikYilBaslangici}").AsNoTracking().ToListAsync(ct);
                var added = new List<Donem>();
                for (var year = start; year <= current.AkademikYilBaslangici; year++)
                foreach (var semester in new[] { AkademikYariyil.Guz, AkademikYariyil.Bahar })
                {
                    var candidate = new Donem { Id = Guid.NewGuid(), AkademikYilBaslangici = year, Yariyil = semester,
                        Kod = $"donem-{year}-{(int)semester}", Ad = $"{year}–{year + 1} {(semester == AkademikYariyil.Guz ? "Güz" : "Bahar")}" };
                    if (KabuldenOnceDegil(candidate, program) && OncekiDonem(candidate, current) &&
                        !existing.Any(x => x.AkademikYilBaslangici == year && x.Yariyil == semester)) added.Add(candidate);
                }
                if (added.Count > 0)
                {
                    db.Donemler.AddRange(added);
                    db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(Donem), VarlikAnahtari = programId.ToString(),
                        Islem = "GecmisDonemSecenekleriniHazirla", YapanKullaniciId = actorId, IslemAni = clock.GetUtcNow(),
                        YeniDeger = JsonSerializer.Serialize(added.Select(x => new { x.Id, x.AkademikYilBaslangici, x.Yariyil, x.Ad })) });
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct); complete = true; return DersDenemeSonucu.Basarili;
            }
            finally { if (!complete) await tx.RollbackAsync(CancellationToken.None); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return DersDenemeSonucu.Cakisma; }
        catch (Exception ex) { logger.LogWarning("Geçmiş dönem seçenekleri hazırlanamadı: {HataTuru}.", ex.GetType().Name); return DersDenemeSonucu.Basarisiz; }
        finally { if (!complete) foreach (var entry in db.ChangeTracker.Entries().Where(x => !initial.Contains(x.Entity)).ToArray()) entry.State = EntityState.Detached; }
    }
}
