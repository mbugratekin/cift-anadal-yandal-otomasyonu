using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Belgeler;

// Yalnız görünüm durumu değişir; içerik, dosya, sürüm ve onay alanlarına dokunulmaz.
public sealed class GenelBelgeArsivServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<GenelBelgeArsivServisi> logger)
{
    public async Task<GenelBelgeKayitKodu> KaydetAsync(Guid actorId, Guid belgeId, byte[]? version, bool arsivle, CancellationToken ct = default)
    {
        if(actorId == Guid.Empty || belgeId == Guid.Empty || version is not { Length:8 }) return GenelBelgeKayitKodu.Gecersiz;
        if(db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return GenelBelgeKayitKodu.Basarisiz;
        Belge? document=null; DegisiklikGecmisi? audit=null; bool completed=false;
        try
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var actor=await users.FindByIdAsync(actorId.ToString());
            if(actor is null) return GenelBelgeKayitKodu.IzinYok;
            await db.Entry(actor).ReloadAsync(ct);
            var permission=await access.OkuAsync(actor);
            if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator)) return GenelBelgeKayitKodu.IzinYok;
            document=await db.Belgeler.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==belgeId && b.Kapsam==BelgeKapsami.Genel && b.GenelBelgeId!=null && b.SurumNo!=null,ct);
            if(document is null) return GenelBelgeKayitKodu.Bulunamadi;
            if(!document.RowVersion.SequenceEqual(version) || document.GenelArsivdeMi==arsivle) return GenelBelgeKayitKodu.Cakisma;
            var tracked=db.ChangeTracker.Entries<Belge>().SingleOrDefault(e=>e.Entity.Id==belgeId);
            if(tracked is not null) tracked.State=EntityState.Detached;
            db.Attach(document);
            var before=document.GenelArsivdeMi;document.GenelArsivdeMi=arsivle;
            audit=new(){Id=Guid.NewGuid(),VarlikTuru=nameof(Belge),VarlikAnahtari=belgeId.ToString(),Islem=arsivle?"GenelBelgeArsivlendi":"GenelBelgeArsivdenAlindi",YapanKullaniciId=actorId,IslemAni=clock.GetUtcNow(),
                OncekiDeger=JsonSerializer.Serialize(new { Arsivde=before,document.GenelBelgeId,document.SurumNo }),YeniDeger=JsonSerializer.Serialize(new { Arsivde=arsivle,document.GenelBelgeId,document.SurumNo })};
            db.DegisiklikGecmisi.Add(audit);
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);completed=true;
            return GenelBelgeKayitKodu.Basarili;
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(DbUpdateConcurrencyException){return GenelBelgeKayitKodu.Cakisma;}
        catch(Exception ex){logger.LogWarning("Genel belge arşiv işlemi tamamlanamadı: {Tur}",ex.GetType().Name);return GenelBelgeKayitKodu.Basarisiz;}
        finally{if(!completed){if(document is not null)db.Entry(document).State=EntityState.Detached;if(audit is not null)db.Entry(audit).State=EntityState.Detached;}}
    }
}
