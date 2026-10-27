using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.DersKaydi;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Belgeler;

public enum EvrakKayitKodu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }
public sealed record EvrakKayitCevabi(EvrakKayitKodu Kod,Guid? BelgeId=null);

public sealed class EvrakKayitServisi(AppDbContext db,UserManager<UygulamaKullanicisi> users,HesapErisimi access,
    BelgeDeposu depot,DersListeServisi lists,TimeProvider clock,ILogger<EvrakKayitServisi> logger)
{
    public Task<EvrakKayitCevabi> GonderAsync(Guid actor,Guid program,Guid period,Guid? list,IFormFile? file,CancellationToken ct=default) =>
        GonderAsync(actor,program,period,list,file,KisiselBelgeTuru.Diger,null,null,ct);

    public Task<EvrakKayitCevabi> GonderAsync(Guid actor,Guid program,Guid period,Guid? list,IFormFile? file,
        KisiselBelgeTuru tur,Guid? seriId,Guid? istekId,CancellationToken ct=default,string? yenilemeGerekcesi=null) =>
        GonderIcAsync(actor,program,period,list,file,tur,seriId,istekId,yenilemeGerekcesi,ct);

    private async Task<EvrakKayitCevabi> GonderIcAsync(Guid actor,Guid program,Guid period,Guid? list,IFormFile? file,
        KisiselBelgeTuru tur,Guid? seriId,Guid? istekId,string? yenilemeGerekcesi,CancellationToken ct)
    {
        if(db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return new(EvrakKayitKodu.Basarisiz);
        var onKontrol=await OnDogrulaAsync(actor,program,period,list,file,tur,seriId,istekId,yenilemeGerekcesi,ct);
        if(onKontrol!=EvrakKayitKodu.Basarili) return new(onKontrol);
        // Dosya tarandıktan sonra yetki ve kayıt bilgileri yeniden kontrol edilir.
        DepolananBelge? hazirDosya=null;
        try { hazirDosya=await depot.KaydetAsync(file,ct); }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(InvalidDataException) { return new(EvrakKayitKodu.Gecersiz); }
        catch(Exception) { logger.LogWarning("Kişisel evrak dosyası hazırlanamadı."); return new(EvrakKayitKodu.Basarisiz); }
        var islemeGirildi=false;
        try {
            return await IslemAsync(written=> {
                islemeGirildi=true;
                written(hazirDosya);
                return YazAsync(actor,program,period,list,file,false,written,tur,seriId,istekId,yenilemeGerekcesi,ct,hazirDosya);
            },ct);
        } finally {
            if(!islemeGirildi && hazirDosya is not null) {
                try { depot.Kaldir(hazirDosya.Anahtar); }
                catch(IOException) { logger.LogWarning("Evrak ön dosyası temizlenemedi."); }
            }
        }
    }

    // Liste ve eki aynı DB işlemi: hatalı dosya, yarım ders listesi bırakmaz.
    public async Task<DersListeCevabi> ListeyleGonderAsync(Guid actor,DersGonderGirdisi input,IFormFile? file,CancellationToken ct=default)
    {
        if(file is null) return await lists.GonderAsync(actor,input,ct);
        if(db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return new(DersListeSonucu.Basarisiz);
        var check=await OnDogrulaAsync(actor,input.ProgramId,input.DonemId,null,file,KisiselBelgeTuru.Diger,null,null,null,ct);
        if(check!=EvrakKayitKodu.Basarili) return new(check==EvrakKayitKodu.IzinYok?DersListeSonucu.IzinYok:
            check==EvrakKayitKodu.Bulunamadi?DersListeSonucu.Bulunamadi:DersListeSonucu.Gecersiz);
        DepolananBelge stored;
        try { stored=await depot.KaydetAsync(file,ct); }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch(InvalidDataException) { return new(DersListeSonucu.Gecersiz); }
        catch(Exception) { logger.LogWarning("Ders listesi eki hazırlanamadı.");return new(DersListeSonucu.Basarisiz); }
        DersListeCevabi? result=null;
        EvrakKayitCevabi upload;
        var entered=false;
        try {
            upload=await IslemAsync(async written=> {
                entered=true;written(stored);
                result=await lists.GonderAsync(actor,input,ct);
                if(result.Sonuc!=DersListeSonucu.Basarili || result.ListeId is null) return new(EvrakKayitKodu.Basarisiz);
                return await YazAsync(actor,input.ProgramId,input.DonemId,result.ListeId,file,true,written,KisiselBelgeTuru.Diger,null,null,null,ct,stored);
            },ct);
        } finally {
            if(!entered) {
                try { depot.Kaldir(stored.Anahtar); }
                catch(IOException) { logger.LogWarning("Ders listesi ön eki temizlenemedi."); }
            }
        }
        if(result is { Sonuc:not DersListeSonucu.Basarili }) return result;
        return upload.Kod==EvrakKayitKodu.Basarili?result!:new(upload.Kod switch {
            EvrakKayitKodu.Gecersiz=>DersListeSonucu.Gecersiz,EvrakKayitKodu.IzinYok=>DersListeSonucu.IzinYok,
            EvrakKayitKodu.Bulunamadi=>DersListeSonucu.Bulunamadi,EvrakKayitKodu.Cakisma=>DersListeSonucu.Cakisma,
            _=>DersListeSonucu.Basarisiz });
    }

    private async Task<EvrakKayitKodu> OnDogrulaAsync(Guid actorId,Guid programId,Guid periodId,Guid? listId,IFormFile? file,
        KisiselBelgeTuru tur,Guid? seriId,Guid? istekId,string? yenilemeGerekcesi,CancellationToken ct)
    {
        if(actorId==Guid.Empty || programId==Guid.Empty || periodId==Guid.Empty || listId==Guid.Empty || file is null ||
            !Enum.IsDefined(tur) || seriId==Guid.Empty || istekId==Guid.Empty || yenilemeGerekcesi?.Length>2000)
            return EvrakKayitKodu.Gecersiz;
        var actor=await users.FindByIdAsync(actorId.ToString()); if(actor is null) return EvrakKayitKodu.IzinYok;
        await db.Entry(actor).ReloadAsync(ct); var permission=await access.OkuAsync(actor);
        if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Any(r=>r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator))
            return EvrakKayitKodu.IzinYok;
        bool student=permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        var period=await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==periodId,ct);
        var current=await db.Donemler.AsNoTracking().SingleOrDefaultAsync(x=>x.IslemDonemiMi,ct);
        if(period is null) return EvrakKayitKodu.Bulunamadi;
        if(current is null || period.AkademikYilBaslangici>current.AkademikYilBaslangici ||
            period.AkademikYilBaslangici==current.AkademikYilBaslangici && period.Yariyil>current.Yariyil)
            return EvrakKayitKodu.Gecersiz;
        var program=await db.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==programId,ct);
        if(program is null || program.HedefBolum!="Bilgisayar Mühendisliği" || student && (program.OgrenciId!=actorId || !program.AktifMi))
            return EvrakKayitKodu.IzinYok;
        if(istekId is { } requestId) {
            var request=await db.EkBelgeIstekleri.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==requestId,ct);
            if(request is null || request.ProgramId!=programId || request.DonemId!=periodId || request.BelgeTuru!=tur) return EvrakKayitKodu.IzinYok;
            if(student && request.Durum is EkBelgeIstegiDurumu.Onaylandi or EkBelgeIstegiDurumu.IptalEdildi) return EvrakKayitKodu.IzinYok;
            var responseSeries=await db.Belgeler.AsNoTracking().Where(b=>b.EkBelgeIstegiId==requestId).Select(b=>b.KisiselBelgeSerisiId).Distinct().SingleOrDefaultAsync(ct);
            if(responseSeries is not null && seriId is not null && seriId!=responseSeries) return EvrakKayitKodu.Gecersiz;
            seriId ??= responseSeries;
        }
        if(seriId is { } seriesId) {
            var existing=await db.KisiselBelgeSerileri.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==seriesId,ct);
            if(existing is null || existing.ProgramId!=programId || existing.DonemId!=periodId || existing.Tur!=tur) return EvrakKayitKodu.IzinYok;
            if(student && existing.OgrenciKilidi) return EvrakKayitKodu.IzinYok;
            var last=await db.Belgeler.AsNoTracking().Where(b=>b.KisiselBelgeSerisiId==seriesId).OrderByDescending(b=>b.KisiselSurumNo).FirstOrDefaultAsync(ct);
            if(last is null || last.EkBelgeIstegiId!=istekId || last.KisiselSurumNo is null or int.MaxValue) return EvrakKayitKodu.Cakisma;
            if(student && last.OnayDurumu is BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.IptalEdildi) return EvrakKayitKodu.IzinYok;
            if(!student && (existing.OgrenciKilidi || last.OnayDurumu is BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.IptalEdildi) && string.IsNullOrWhiteSpace(yenilemeGerekcesi)) return EvrakKayitKodu.Gecersiz;
        }
        if(listId is { } id && !await db.DersSecimleri.AsNoTracking().AnyAsync(l=>l.Id==id && l.ProgramId==programId && l.DonemId==periodId,ct))
            return EvrakKayitKodu.Bulunamadi;
        return EvrakKayitKodu.Basarili;
    }

    private async Task<EvrakKayitCevabi> YazAsync(Guid actorId,Guid programId,Guid periodId,Guid? listId,IFormFile? file,bool newList,Action<DepolananBelge> written,
        KisiselBelgeTuru tur,Guid? seriId,Guid? istekId,string? yenilemeGerekcesi,CancellationToken ct,DepolananBelge? hazirDosya=null)
    {
        if(actorId==Guid.Empty || programId==Guid.Empty || periodId==Guid.Empty || listId==Guid.Empty || file is null ||
            !Enum.IsDefined(tur) || seriId==Guid.Empty || istekId==Guid.Empty || yenilemeGerekcesi?.Length>2000)
            return new(EvrakKayitKodu.Gecersiz);
        var actor=await users.FindByIdAsync(actorId.ToString()); if(actor is null) return new(EvrakKayitKodu.IzinYok);
        await db.Entry(actor).ReloadAsync(ct); var permission=await access.OkuAsync(actor);
        if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Any(r=>r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator))
            return new(EvrakKayitKodu.IzinYok);
        bool student=permission.Roller.Contains(UygulamaRolleri.Ogrenci);
        // Ders kaydıyla ortak parent sırası: Dönem -> Program -> gönderi.
        var period=await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={periodId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var current=await db.Donemler.FromSqlRaw("SELECT * FROM [Donemler] WITH (HOLDLOCK) WHERE [IslemDonemiMi]=1")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if(period is null) return new(EvrakKayitKodu.Bulunamadi);
        if(current is null || period.AkademikYilBaslangici>current.AkademikYilBaslangici ||
            period.AkademikYilBaslangici==current.AkademikYilBaslangici && period.Yariyil>current.Yariyil)
            return new(EvrakKayitKodu.Gecersiz);
        var program=await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={programId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if(program is null || program.HedefBolum!="Bilgisayar Mühendisliği" || student && (program.OgrenciId!=actorId || !program.AktifMi))
            return new(EvrakKayitKodu.IzinYok);
        EkBelgeIstegi? request=null;
        string? requestBefore=null;
        if(istekId is { } requestId) {
            request=await db.EkBelgeIstekleri.FromSqlInterpolated($"SELECT * FROM [EkBelgeIstekleri] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={requestId}").SingleOrDefaultAsync(ct);
            if(request is null || request.ProgramId!=programId || request.DonemId!=periodId || request.BelgeTuru!=tur) return new(EvrakKayitKodu.IzinYok);
            await db.Entry(request).ReloadAsync(ct);
            if(student && request.Durum is EkBelgeIstegiDurumu.Onaylandi or EkBelgeIstegiDurumu.IptalEdildi) return new(EvrakKayitKodu.IzinYok);
            var responseSeries=await db.Belgeler.Where(b=>b.EkBelgeIstegiId==requestId).Select(b=>b.KisiselBelgeSerisiId).Distinct().SingleOrDefaultAsync(ct);
            if(responseSeries is not null && seriId is not null && seriId!=responseSeries) return new(EvrakKayitKodu.Gecersiz);
            seriId ??= responseSeries;
            requestBefore=BelgeIncelemeServisi.IstekDegeri(request);
        }
        KisiselBelgeSerisi series;
        var version=1;
        if(seriId is { } seriesId) {
            var existing=await db.KisiselBelgeSerileri.FromSqlInterpolated($"SELECT * FROM [KisiselBelgeSerileri] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={seriesId}").SingleOrDefaultAsync(ct);
            if(existing is null || existing.ProgramId!=programId || existing.DonemId!=periodId || existing.Tur!=tur) return new(EvrakKayitKodu.IzinYok);
            series=existing;
            if(student && series.OgrenciKilidi) return new(EvrakKayitKodu.IzinYok);
            var last=await db.Belgeler.AsNoTracking().Where(b=>b.KisiselBelgeSerisiId==seriesId).OrderByDescending(b=>b.KisiselSurumNo).FirstOrDefaultAsync(ct);
            if(last is null || last.EkBelgeIstegiId!=istekId || last.KisiselSurumNo is null or int.MaxValue) return new(EvrakKayitKodu.Cakisma);
            if(student && last.OnayDurumu is BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.IptalEdildi) return new(EvrakKayitKodu.IzinYok);
            if(!student && (series.OgrenciKilidi || last.OnayDurumu is BelgeOnayDurumu.Onaylandi or BelgeOnayDurumu.IptalEdildi) && string.IsNullOrWhiteSpace(yenilemeGerekcesi)) return new(EvrakKayitKodu.Gecersiz);
            version=last.KisiselSurumNo.Value+1;
        } else {
            series=new() {Id=Guid.NewGuid(),ProgramId=programId,DonemId=periodId,Tur=tur,OlusturanKullaniciId=actorId,OlusturulmaAni=clock.GetUtcNow()};
            db.KisiselBelgeSerileri.Add(series);
        }
        EvrakGonderimi? submission=null;
        if(listId is { } id) {
            var list=await db.DersSecimleri.AsNoTracking().SingleOrDefaultAsync(l=>l.Id==id && l.ProgramId==programId && l.DonemId==periodId,ct);
            if(list is null) return new(EvrakKayitKodu.Bulunamadi);
            // Dönem sonu transkripti mevcut listeye de eklenebilir; program sahipliği ve belge kilidi yukarıda doğrulanır.
            if(student && newList && list.GonderenKullaniciId!=actorId) return new(EvrakKayitKodu.IzinYok);
        } else {
            submission=new() {Id=Guid.NewGuid(),ProgramId=programId,DonemId=periodId,GonderenKullaniciId=actorId,GonderimAni=clock.GetUtcNow()};
        }
        var stored=hazirDosya ?? await depot.KaydetAsync(file,ct);
        if(hazirDosya is null) written(stored);
        var document=new Belge {Id=Guid.NewGuid(),Kapsam=BelgeKapsami.Kisisel,Tur="Kişisel evrak",ProgramId=programId,
            KisiselBelgeSerisiId=series.Id,KisiselSurumNo=version,KisiselTur=tur,ArsivDonemId=periodId,EkBelgeIstegiId=istekId,OnayDurumu=BelgeOnayDurumu.OnayBekliyor,
            DersSecimiId=listId,EvrakGonderimiId=submission?.Id,YukleyenKullaniciId=actorId,YuklemeAni=clock.GetUtcNow(),
            DosyaAdi=file.FileName,DepolamaAnahtari=stored.Anahtar,IcerikTuru=stored.Mime,BoyutBayt=stored.Boyut,Sha256=stored.Sha256,
            IcerikDurumu=BelgeIcerikDurumu.Mevcut};
        if(submission is not null) db.EvrakGonderimleri.Add(submission);
        db.Belgeler.Add(document);
        db.DegisiklikGecmisi.Add(new() {Id=Guid.NewGuid(),VarlikTuru="Belge",VarlikAnahtari=document.Id.ToString(),Islem="KisiselEvrakGonder",
            YapanKullaniciId=actorId,IslemAni=document.YuklemeAni,YeniDeger=JsonSerializer.Serialize(new {ProgramId=programId,DonemId=periodId,BelgeId=document.Id,document.KisiselBelgeSerisiId,document.KisiselSurumNo,document.KisiselTur,document.OnayDurumu,document.EkBelgeIstegiId,YenilemeGerekcesi=yenilemeGerekcesi?.Trim()})});
        if(request is not null) {
            request.Durum=EkBelgeIstegiDurumu.Yuklendi;request.KararAni=null;request.KararVerenKullaniciId=null;request.KararGerekcesi=null;
            db.DegisiklikGecmisi.Add(new() {Id=Guid.NewGuid(),VarlikTuru="EkBelgeIstegi",VarlikAnahtari=request.Id.ToString(),Islem="BelgeIstegineYanit",
                YapanKullaniciId=actorId,IslemAni=document.YuklemeAni,OncekiDeger=requestBefore,YeniDeger=BelgeIncelemeServisi.IstekDegeri(request)});
        }
        // Kayıt sonucu belirsizse diskteki dosya doğrudan silinmez.
        await db.SaveChangesAsync(ct); return new(EvrakKayitKodu.Basarili,document.Id);
    }
    private async Task<EvrakKayitCevabi> IslemAsync(Func<Action<DepolananBelge>,Task<EvrakKayitCevabi>> work,CancellationToken ct)
    {
        if(db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null) return new(EvrakKayitKodu.Basarisiz);
        try {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            bool completed=false,commitStarted=false;
            var files=new List<DepolananBelge>();
            try {
                var result=await work(files.Add);
                if(result.Kod==EvrakKayitKodu.Basarili) {commitStarted=true;await tx.CommitAsync(ct);completed=true;}
                return result;
            } finally {
                if(!completed) {
                    await tx.RollbackAsync(CancellationToken.None);
                    db.ChangeTracker.Clear();
                    // Yalnız kesin geri alınmış, commit denenmemiş bu çağrının dosyaları.
                    if(!commitStarted) foreach(var file in files) {
                        try {depot.Kaldir(file.Anahtar);}catch(IOException) {logger.LogWarning("Evrak metadata geri alındı; yeni dosya telafisi tamamlanamadı.");}
                    }
                }
            }
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
        catch(InvalidDataException) {return new(EvrakKayitKodu.Gecersiz);}
        catch(DbUpdateConcurrencyException) {return new(EvrakKayitKodu.Cakisma);}
        catch(Exception) {logger.LogWarning("Kişisel evrak işlemi tamamlanamadı; dosya/commit durumu doğrulanmalı.");return new(EvrakKayitKodu.Basarisiz);}
    }
}
