using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Services.Belgeler;
public sealed class EvrakOkumaServisi(AppDbContext db,UserManager<UygulamaKullanicisi> users,HesapErisimi access)
{
    public async Task<EvrakSayfaModel?> OkuAsync(Guid actorId,Guid? programId,int? year,int? semester,Guid? listId=null,CancellationToken ct=default,bool gecmisiEkle=true)
    {
        var actor=await users.FindByIdAsync(actorId.ToString()); if(actor is null) return null;
        await db.Entry(actor).ReloadAsync(ct); var permission=await access.OkuAsync(actor);
        if(!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Any(r=>r is UygulamaRolleri.Ogrenci or UygulamaRolleri.Koordinator or UygulamaRolleri.BolumBaskani)) return null;
        bool student=permission.Roller.Contains(UygulamaRolleri.Ogrenci),coordinator=permission.Roller.Contains(UygulamaRolleri.Koordinator);
        var current=await db.Donemler.AsNoTracking().SingleOrDefaultAsync(d=>d.IslemDonemiMi,ct);
        int sem=semester??(int?)current?.Yariyil??1;
        int y=year??(current is null?DateTime.UtcNow.Year:current.AkademikYilBaslangici+(current.Yariyil==AkademikYariyil.Bahar?1:0));
        if(y is <2 or >9998 || sem is <1 or >2) return null;
        int academic=sem==2?y-1:y;
        var period=await db.Donemler.AsNoTracking().SingleOrDefaultAsync(d=>d.AkademikYilBaslangici==academic && (int)d.Yariyil==sem,ct);
        bool validPeriod=period is not null && current is not null && (academic<current.AkademikYilBaslangici || academic==current.AkademikYilBaslangici && sem<=(int)current.Yariyil);
        var programs=await (from p in db.OgrenciProgramlari.AsNoTracking() join u in db.Users.AsNoTracking() on p.OgrenciId equals u.Id
            where p.HedefBolum=="Bilgisayar Mühendisliği" && (!student || p.OgrenciId==actorId) && (programId==null || p.Id==programId)
            orderby u.OgrenciNo select new {p.Id,p.AktifMi,u.OgrenciNo,Ad=((u.Ad??"")+" "+(u.Soyad??"")).Trim()}).ToListAsync(ct);
        if(programId is not null && programs.Count==0) return null;
        var result=new List<EvrakBaglamModel>();
        foreach(var p in programs) {
            var lockedSeries=await db.KisiselBelgeSerileri.AsNoTracking().Where(s=>s.ProgramId==p.Id && s.OgrenciKilidi).Select(s=>s.Id).ToListAsync(ct);
            var docs=period is null?[]:await db.Belgeler.AsNoTracking().Where(b=>b.Kapsam==BelgeKapsami.Kisisel && b.ProgramId==p.Id &&
                b.GenelBelgeId==null && b.SurumNo==null && b.ResmiBaglanti==null &&
                (listId!=null ? b.DersSecimiId==listId && b.EvrakGonderimiId==null && db.DersSecimleri.Any(l=>l.Id==listId && l.ProgramId==p.Id && l.DonemId==period.Id) :
                    b.DersSecimiId==null && b.EvrakGonderimiId!=null && db.EvrakGonderimleri.Any(e=>e.Id==b.EvrakGonderimiId && e.ProgramId==p.Id && e.DonemId==period.Id) ||
                    b.DersSecimiId!=null && b.EvrakGonderimiId==null && db.DersSecimleri.Any(l=>l.Id==b.DersSecimiId && l.ProgramId==p.Id && l.DonemId==period.Id)))
                .OrderBy(b=>b.YuklemeAni).ToListAsync(ct);
            if(listId is not null && (period is null || !await db.DersSecimleri.AnyAsync(l=>l.Id==listId && l.ProgramId==p.Id && l.DonemId==period.Id,ct))) continue;
            result.Add(new(p.Id,period?.Id,listId,p.Ad,$"{y} {(sem==1?"Güz":"Bahar")}",validPeriod && (coordinator || student && p.AktifMi && listId==null),
                docs.Select(b=>new EvrakDosyaModel(b.Id,b.DosyaAdi??"Dosya",b.BoyutBayt,b.IcerikDurumu switch {BelgeIcerikDurumu.Mevcut=>b.Sha256==null?"Dosya doğrulanamıyor":"Mevcut",BelgeIcerikDurumu.Temizleniyor=>"Kaldırma tamamlanmadı",_=>"İçerik kaldırıldı"},
                    Convert.ToBase64String(b.RowVersion),b.IcerikDurumu==BelgeIcerikDurumu.Mevcut && b.Sha256!=null,false)
                {SeriId=b.KisiselBelgeSerisiId,IstekId=b.EkBelgeIstegiId,SurumNo=b.KisiselSurumNo,Tur=b.KisiselTur,Onay=b.OnayDurumu,Gerekce=b.KararGerekcesi,OgrenciKilidi=b.KisiselBelgeSerisiId is { } seriesId && lockedSeries.Contains(seriesId),
                    YuklemeAni=b.YuklemeAni,GuncelMi=b.KisiselBelgeSerisiId is null || !docs.Any(newer=>newer.KisiselBelgeSerisiId==b.KisiselBelgeSerisiId && newer.KisiselSurumNo>b.KisiselSurumNo)}).ToList())
            {OgrenciNo=p.OgrenciNo,Inceleyebilir=coordinator,Istekler=period is null?[]:await db.EkBelgeIstekleri.AsNoTracking().Where(r=>r.ProgramId==p.Id && r.DonemId==period.Id)
                .OrderBy(r=>r.IstekAni).Select(r=>new EkBelgeIstekSatiri(r.Id,r.BelgeTuru,r.IstekMetni,BelgeArsivKurali.IstekDurumuAdi(r.Durum),r.IstekAni,r.KararGerekcesi)
                {DurumKodu=r.Durum,OgrenciKilidi=db.Belgeler.Any(b=>b.EkBelgeIstegiId==r.Id && db.KisiselBelgeSerileri.Any(s=>s.Id==b.KisiselBelgeSerisiId && s.OgrenciKilidi))}).ToListAsync(ct)});
        }
        if (gecmisiEkle && year is null && semester is null && listId is null && current is not null)
        {
            for (var i = 0; i < result.Count; i++)
            {
                var context = result[i];
                var periods = await db.Donemler.AsNoTracking().Where(d => d.Id != context.DonemId &&
                    (d.AkademikYilBaslangici < current.AkademikYilBaslangici || d.AkademikYilBaslangici == current.AkademikYilBaslangici && d.Yariyil <= current.Yariyil) &&
                    (db.EvrakGonderimleri.Any(e => e.ProgramId == context.ProgramId && e.DonemId == d.Id && db.Belgeler.Any(b => b.EvrakGonderimiId == e.Id)) ||
                     db.DersSecimleri.Any(l => l.ProgramId == context.ProgramId && l.DonemId == d.Id && db.Belgeler.Any(b => b.DersSecimiId == l.Id)) ||
                     db.EkBelgeIstekleri.Any(r => r.ProgramId == context.ProgramId && r.DonemId == d.Id)))
                    .OrderByDescending(d => d.AkademikYilBaslangici).ThenByDescending(d => d.Yariyil).ToListAsync(ct);
                var history = new List<EvrakBaglamModel>();
                foreach (var d in periods)
                {
                    var page = await OkuAsync(actorId, context.ProgramId, d.AkademikYilBaslangici + (d.Yariyil == AkademikYariyil.Bahar ? 1 : 0), (int)d.Yariyil, null, ct, false);
                    var entry = page?.Baglamlar.SingleOrDefault();
                    if (entry is not null && (entry.Dosyalar.Count > 0 || entry.Istekler.Count > 0))
                        history.Add(entry with { GecmisDonemMi = true });
                }
                result[i] = context with { GecmisDonemler = history };
            }
        }
        return new(y,sem,current is not null,result);
    }
}
