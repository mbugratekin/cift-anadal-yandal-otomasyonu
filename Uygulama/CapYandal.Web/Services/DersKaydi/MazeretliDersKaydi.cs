using System.Data;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.DersKaydi;

public static class MazeretliDersKaydi
{
    // Liste oluşturma/düzeltme ve onay birlikte gerçekleşir; öğrenci için kapı açılmaz.
    public static async Task<DersListeCevabi> KaydetAsync(AppDbContext db, DersListeServisi lists,
        DersOnayServisi approvals, Guid actor, DersListeKomutModel input, CancellationToken ct)
    {
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is { } external && external.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return new(DersListeSonucu.Basarisiz);
        await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var tx = own ?? db.Database.CurrentTransaction!;
        var point = "M" + Guid.NewGuid().ToString("N")[..24];
        if (own is null) await tx.CreateSavepointAsync(point, ct);
        var complete = false;
        try
        {
            var result = await lists.MazeretliAsync(actor, input.ProgramId, input.DonemId,
                input.ListeId == Guid.Empty ? null : input.ListeId, input.ListeSurum, input.SablonSurum,
                input.Dersler ?? [], input.Gerekce ?? "", ct);
            if (result.Sonuc != DersListeSonucu.Basarili) return result;
            var version = await db.DersSecimleri.AsNoTracking().Where(x => x.Id == result.ListeId).Select(x => x.RowVersion).SingleAsync(ct);
            var approved = await approvals.OnaylaAsync(actor, new(result.ListeId!.Value, Convert.ToBase64String(version),
                DersSecimiDurumu.Onaylandi, input.Gerekce, Mazeretli: true), ct);
            if (approved != DersOnaySonucu.Basarili) return new(approved switch {
                DersOnaySonucu.IzinYok => DersListeSonucu.IzinYok, DersOnaySonucu.Cakisma => DersListeSonucu.Cakisma,
                DersOnaySonucu.Bulunamadi => DersListeSonucu.Bulunamadi, _ => DersListeSonucu.Gecersiz });
            if (own is not null) await tx.CommitAsync(ct); complete = true; return result;
        }
        catch (DbUpdateException) { return new(DersListeSonucu.Cakisma); }
        finally { if (!complete) { if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None); else await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); } }
    }
}
