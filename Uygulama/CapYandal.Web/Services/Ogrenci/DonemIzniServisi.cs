using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Ogrenci;

public static class AkademikDonemSirasi
{
    public static int Kod(int yil, int yariyil) => yil is >= 1 and <= 9998 && yariyil is 1 or 2
        ? yil * 2 + yariyil - 1 : throw new ArgumentOutOfRangeException(nameof(yil));
    public static string Ad(int kod) => $"{kod / 2}–{kod / 2 + 1} {(kod % 2 == 0 ? "Güz" : "Bahar")}";
    public static bool Kapsar(int baslangic, int bitis, int donem) => baslangic <= donem && donem <= bitis;
}

// İzin aralığı: program kilidi çakışmayı önler; iptal fiziksel silme değildir.
public sealed class DonemIzniServisi(AppDbContext db, HesapErisimi erisim, TimeProvider saat,
    ILogger<DonemIzniServisi> logger)
{
    public async Task<DonemYonetimiPaneli?> OkuAsync(Guid actor, Guid programId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, ct);
        var program = await db.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId && x.HedefBolum == "Bilgisayar Mühendisliği", ct);
        if (user is null || program is null) return null;
        var access = await erisim.OkuAsync(user);
        if (!access.Izinli || access.ParolaDegisikligiGerekli || access.Roller.Length != 1 ||
            access.Roller[0] == UygulamaRolleri.Ogrenci && program.OgrenciId != actor) return null;
        var rows = await db.ProgramDonemIzinleri.AsNoTracking().Where(x => x.ProgramId == programId)
            .OrderByDescending(x => x.Baslangic).ThenByDescending(x => x.KayitAni).ToListAsync(ct);
        var current = await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
        var durum = current is null ? "İşlem dönemi belirlenmemiş." : rows.FirstOrDefault(x => x.IptalAni == null &&
            AkademikDonemSirasi.Kapsar(x.Baslangic, x.Bitis, AkademikDonemSirasi.Kod(current.AkademikYilBaslangici, (int)current.Yariyil)))?.Tur;
        var documents = access.Roller[0] == UygulamaRolleri.Koordinator ? await db.Belgeler.AsNoTracking()
            .Where(x => x.ProgramId == programId && x.Kapsam == BelgeKapsami.Kisisel)
            .OrderByDescending(x => x.YuklemeAni).Select(x => new DonemBelgeSecenegi(x.Id, (x.Tur ?? "Belge") + " / " + (x.DosyaAdi ?? "Dosya"))).ToArrayAsync(ct) : [];
        return new DonemYonetimiPaneli(programId, rows.Select(x => new DonemIzniSatiri(x.Id, AkademikDonemSirasi.Ad(x.Baslangic),
            AkademikDonemSirasi.Ad(x.Bitis), x.Tur == "Izin" ? "İzinli" : "Kayıt donduruldu", x.Gerekce,
            x.BelgeId, x.IptalAni != null, x.IptalGerekcesi, Convert.ToBase64String(x.RowVersion))).ToArray(),
            current is null ? durum : durum == "Izin" ? "İzinli" : durum == "Dondurma" ? "Kayıt donduruldu" : "Normal") { Belgeler = documents };
    }

    public async Task<int> KaydetAsync(Guid actor, DonemIzniModel input, bool iptal, CancellationToken ct = default)
    {
        if (actor == Guid.Empty || input.ProgramId == Guid.Empty || string.IsNullOrWhiteSpace(input.Gerekce) || input.Gerekce.Length > 2000)
            return 400;
        if (!iptal && (input.Tur != "Dondurma" || input.BaslangicYili is < 1 or > 9998 || input.BitisYili is < 1 or > 9998 ||
            input.BaslangicYariyili is < 1 or > 2 || input.BitisYariyili is < 1 or > 2)) return 400;
        if (iptal && (input.Id is null || input.Id == Guid.Empty || input.Surum is not { Length: 12 })) return 400;
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return 503;
        try
        {
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor, ct);
            if (user is null) return 403;
            var access = await erisim.OkuAsync(user);
            if (!access.Izinli || access.ParolaDegisikligiGerekli || !access.Roller.SequenceEqual([UygulamaRolleri.Koordinator])) return 403;
            var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={input.ProgramId}").SingleOrDefaultAsync(ct);
            if (program is null || program.HedefBolum != "Bilgisayar Mühendisliği") return 404;
            ProgramDonemIzni row; string? before = null;
            if (iptal)
            {
                row = (await db.ProgramDonemIzinleri.SingleOrDefaultAsync(x => x.Id == input.Id && x.ProgramId == program.Id, ct))!;
                if (row is null) return 404;
                if (row.IptalAni != null || Convert.ToBase64String(row.RowVersion) != input.Surum) return 409;
                before = JsonSerializer.Serialize(row);
                row.IptalAni = saat.GetUtcNow(); row.IptalEdenId = actor; row.IptalGerekcesi = input.Gerekce.Trim();
            }
            else
            {
                var start = AkademikDonemSirasi.Kod(input.BaslangicYili, input.BaslangicYariyili);
                var end = AkademikDonemSirasi.Kod(input.BitisYili, input.BitisYariyili);
                if (end < start || program.KabulAkademikYilBaslangici is not int kabul || program.KabulYariyili is null ||
                    start < AkademikDonemSirasi.Kod(kabul, (int)program.KabulYariyili)) return 400;
                if (await db.ProgramDonemIzinleri.AnyAsync(x => x.ProgramId == program.Id && x.IptalAni == null && x.Baslangic <= end && start <= x.Bitis, ct)) return 409;
                if (input.BelgeId is not null && !await db.Belgeler.AnyAsync(x => x.Id == input.BelgeId && x.ProgramId == program.Id &&
                    x.Kapsam != BelgeKapsami.Genel, ct)) return 400;
                row = new() { Id = Guid.NewGuid(), ProgramId = program.Id, Baslangic = start, Bitis = end, Tur = input.Tur,
                    Gerekce = input.Gerekce.Trim(), BelgeId = input.BelgeId, KaydedenId = actor, KayitAni = saat.GetUtcNow() };
                db.ProgramDonemIzinleri.Add(row);
            }
            db.DegisiklikGecmisi.Add(new() { Id = Guid.NewGuid(), VarlikTuru = nameof(ProgramDonemIzni), VarlikAnahtari = row.Id.ToString(),
                YapanKullaniciId = actor, IslemAni = saat.GetUtcNow(), Islem = iptal ? "DonemIzniIptal" : "DonemIzniEkle",
                OncekiDeger = before, YeniDeger = JsonSerializer.Serialize(row) });
            await db.SaveChangesAsync(ct);
            if (own is not null) await tx.CommitAsync(ct);
            completed = true; return 200;
            }
            finally
            {
                if (!completed)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await tx.RollbackAsync(CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return 409; }
        catch (Exception e) { logger.LogWarning("Dönem izni kaydedilemedi: {Tur}", e.GetType().Name); return 503; }
        finally { db.ChangeTracker.Clear(); }
    }
}
