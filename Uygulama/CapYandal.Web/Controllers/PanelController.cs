using CapYandal.Web.Security;
using CapYandal.Web.Models;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CapYandal.Web.Services.Ogrenci;
using CapYandal.Web.Services.Paneller;

namespace CapYandal.Web.Controllers;

[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PanelController(AppDbContext database, UserManager<UygulamaKullanicisi> users,
    CapYandal.Web.Services.Gano.GanoEkranServisi ganoEkrani, Services.Hesap.GorusmeBilgisiServisi gorusme,
    PanelModelServisi panelModelleri, Services.DersKaydi.DersListeOkumaServisi dersler, DonemIzniServisi donemIzinleri,
    Services.Belgeler.KisiselBelgeOkumaServisi belgeler) : Controller
{
    private static IReadOnlyList<string> KoordinatorBolumleri => PanelBolumleri.Koordinator;
    private static IReadOnlyList<string> BaskanBolumleri => PanelBolumleri.Baskan;

    // Rolüne göre ana paneli açar.
    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci)]
    public Task<IActionResult> Ogrenci() => Goster("Öğrenci", ["Ders Kaydı ve Evrak İşlemleri", "Onay Takip", "Sohbet", "Dokümanlar"], true);
    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator)]
    public IActionResult Koordinator() => RedirectToAction(nameof(Ogrenciler));
    [HttpGet, Authorize(Roles = UygulamaRolleri.BolumBaskani)]
    public IActionResult BolumBaskani() => RedirectToAction(nameof(Ogrenciler));

    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Öğrencileri numara, ad, program türü ve duruma göre listeler.
    public async Task<IActionResult> Ogrenciler(string? arama, string? tur, string? durum)
    {
        arama = arama?.Trim();
        if (arama?.Length > 100 || tur is not (null or "" or "Cap" or "YanDal") || durum is not (null or "" or "Aktif" or "Pasif"))
            return BadRequest("Geçerli filtre seçin; arama en fazla 100 karakter olabilir.");
        var coordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var model = await PaneliHazirla(coordinator ? "Koordinatör" : "Bölüm Başkanı",
            coordinator ? KoordinatorBolumleri : BaskanBolumleri);
        if (model is null) return Challenge();
        // Pasif programlar da geçmiş kayıt olarak listede kalır.
        var query = from program in database.OgrenciProgramlari.AsNoTracking()
                    join student in database.Users.AsNoTracking() on program.OgrenciId equals student.Id
                    where program.HedefBolum == "Bilgisayar Mühendisliği"
                    select new { Program = program, Student = student };
        if (!string.IsNullOrWhiteSpace(arama))
            query = query.Where(row => (row.Student.OgrenciNo ?? "").Contains(arama)
                || ((row.Student.Ad ?? "") + " " + (row.Student.Soyad ?? "")).Contains(arama));
        if (!string.IsNullOrEmpty(tur))
        {
            var type = tur == "Cap" ? OgrenciProgramTuru.Cap : OgrenciProgramTuru.YanDal;
            query = query.Where(row => row.Program.Tur == type);
        }
        if (!string.IsNullOrEmpty(durum)) query = query.Where(row => row.Program.AktifMi == (durum == "Aktif"));
        var records = await query.OrderByDescending(row => row.Program.AktifMi).ThenBy(row => row.Student.OgrenciNo).ThenBy(row => row.Program.BaslangicTarihi)
            .Select(row => new OgrenciSatirModel(row.Program.Id, row.Student.OgrenciNo ?? "—",
                (row.Student.Ad ?? "") + " " + (row.Student.Soyad ?? ""),
                row.Program.Tur == OgrenciProgramTuru.Cap ? "Çift Anadal" : "Yan Dal",
                row.Program.KaynakAnaDal, row.Program.AktifMi)).ToListAsync();
        return View("Ogrenciler", model with
        {
            AktifBolum = "Öğrenciler", Ogrenciler = records, Arama = arama, ProgramTuru = tur, ProgramDurumu = durum
        });
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Öğrencinin program, GANO, belge ve ders bilgilerini açar.
    public async Task<IActionResult> OgrenciAyrintisi(Guid id, CancellationToken cancellationToken)
    {
        var kayit = await OgrenciKaydiniBul(id, cancellationToken);
        if (kayit is null) return NotFound();
        var (program, ogrenci) = kayit.Value;
        var mufredatBilgisi = await (from sablon in database.DersSablonlari.AsNoTracking()
            join mufredat in database.Mufredatlar.AsNoTracking() on sablon.MufredatId equals mufredat.Id into mufredatlar
            from mufredat in mufredatlar.DefaultIfEmpty()
            where sablon.Id == program.MufredatSablonId
            select new { DersListesi = sablon.Ad, Mufredat = mufredat != null ? mufredat.Ad : sablon.MufredatAdi,
                MufredatAktifMi = mufredat == null || mufredat.EtkinMi,
                BaslangicYili = mufredat != null ? mufredat.BaslangicYili : null }).SingleOrDefaultAsync(cancellationToken);
        string? mufredatKapsami = null;
        if (mufredatBilgisi?.BaslangicYili is int mufredatBaslangici)
        {
            var sonrakiBaslangic = await database.Mufredatlar.AsNoTracking()
                .Where(x => x.BaslangicYili > mufredatBaslangici).OrderBy(x => x.BaslangicYili)
                .Select(x => x.BaslangicYili).FirstOrDefaultAsync(cancellationToken);
            mufredatKapsami = sonrakiBaslangic is int sonraki
                ? $"{mufredatBaslangici}–{mufredatBaslangici + 1} ile {sonraki - 1}–{sonraki} kabulleri"
                : $"{mufredatBaslangici}–{mufredatBaslangici + 1} ve sonrası";
        }
        var progressActor = await users.GetUserAsync(User);
        ViewData["DersIlerlemesi"] = await dersler.IlerlemeOkuAsync(progressActor!.Id, program.Id, cancellationToken);
        ViewData["Transkriptler"]=await belgeler.TranskriptlerAsync(progressActor.Id,id,cancellationToken);
        ViewData["KosulDonemId"] = await database.Donemler.AsNoTracking().Where(x => x.IslemDonemiMi).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var koordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var model = await PaneliHazirla(koordinator ? "Koordinatör" : "Bölüm Başkanı", koordinator ? KoordinatorBolumleri : BaskanBolumleri);
        if (model is null) return Challenge();
        var ganoKapsami = program.Tur == OgrenciProgramTuru.Cap ? GanoKapsami.Cap : GanoKapsami.YanDal;
        var ganoKayitlari = await (from g in database.GanoKontrolKayitlari.AsNoTracking()
                          join d in database.Donemler.AsNoTracking() on g.DonemId equals d.Id
                          where g.ProgramId == id && (g.Kapsam == ganoKapsami || g.Kapsam == GanoKapsami.AnaDal)
                          orderby d.AkademikYilBaslangici, d.Yariyil
                          select new { d.AkademikYilBaslangici, d.Yariyil, g.Kapsam, g.Gano, g.RowVersion, g.Kaynak }).ToListAsync(cancellationToken);
        var gano = ganoKayitlari.GroupBy(g => new { g.AkademikYilBaslangici, g.Yariyil })
            .OrderBy(g => g.Key.AkademikYilBaslangici).ThenBy(g => g.Key.Yariyil)
            .Select(g => new GanoSatirModel(g.Key.AkademikYilBaslangici,
                g.Key.Yariyil == AkademikYariyil.Guz ? "Güz" : "Bahar",
                g.Where(k => k.Kapsam == GanoKapsami.AnaDal).Select(k => (decimal?)k.Gano).SingleOrDefault(),
                g.Where(k => k.Kapsam == ganoKapsami).Select(k => (decimal?)k.Gano).SingleOrDefault())
            {
                AnaDalSurum = g.Where(k => k.Kapsam == GanoKapsami.AnaDal).Select(k => Convert.ToBase64String(k.RowVersion)).SingleOrDefault(),
                AnaDalKaynak = g.Where(k => k.Kapsam == GanoKapsami.AnaDal).Select(k => k.Kaynak).SingleOrDefault(),
                ProgramSurum = g.Where(k => k.Kapsam == ganoKapsami).Select(k => Convert.ToBase64String(k.RowVersion)).SingleOrDefault()
            }).ToList();
        var takipNotlari = koordinator ? await database.ProgramTakipNotlari.AsNoTracking()
            .Where(n => n.ProgramId == id && !n.SilindiMi).OrderBy(n => n.OlusturmaAni).ThenBy(n => n.Id)
            .ToListAsync(cancellationToken) : [];
        var display = await ganoEkrani.OkuAsync((await users.GetUserAsync(User))!.Id, id, cancellationToken);
        var currentPeriod = await database.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, cancellationToken);
        var ganoPeriods = new List<string>();
        if (currentPeriod is not null && program.KabulAkademikYilBaslangici is int firstYear && firstYear >= 2 && currentPeriod.AkademikYilBaslangici - firstYear is >= 0 and <= 100)
            for(var year=firstYear; year<=currentPeriod.AkademikYilBaslangici; year++)
                foreach(var semester in new[]{AkademikYariyil.Guz,AkademikYariyil.Bahar})
                    if ((year > firstYear || semester >= (program.KabulYariyili ?? AkademikYariyil.Guz)) &&
                        (year < currentPeriod.AkademikYilBaslangici || semester <= currentPeriod.Yariyil))
                        ganoPeriods.Add($"{year+(semester == AkademikYariyil.Bahar ? 1 : 0)} {(semester == AkademikYariyil.Guz ? "Güz" : "Bahar")}");
        ViewData["GanoDonemSecenekleri"] = ganoPeriods.Concat(gano.Select(g=>$"{g.Yil+(g.Yariyil == "Bahar" ? 1 : 0)} {g.Yariyil}"))
            .Distinct().OrderByDescending(value => GanoKayitServisi.DonemiOku(value, out var year, out var semester)
                ? (semester == AkademikYariyil.Bahar ? year - 1 : year) * 2 + (int)semester : 0).ToArray();
        return View(model with { AktifBolum = "Öğrenciler", GanoGorunumu = display ?? [],
            DonemIzinleri = await donemIzinleri.OkuAsync(progressActor!.Id, id, cancellationToken),
            GanoOkumaHatasi = display is null ? "GANO değerlendirme geçmişi doğrulanamadı." : null,
            OnerilenGanoDonemi = gano.Count == 0 ? null : GanoKayitServisi.SonrakiDonem(gano[^1].Yil,
                gano[^1].Yariyil == "Güz" ? AkademikYariyil.Guz : AkademikYariyil.Bahar),
            TakipNotlari = takipNotlari.Select(n => new ProgramTakipNotuSatirModel(n.Id, n.Metin,
                n.OlusturmaAni, Convert.ToBase64String(n.RowVersion))).ToList(),
            OgrenciAyrintisi = new OgrenciAyrintiModel(id,
            ogrenci.OgrenciNo ?? "—", (ogrenci.Ad + " " + ogrenci.Soyad).Trim(), ogrenci.Email,
            ogrenci.KisiselEposta ?? ogrenci.BekleyenKisiselEposta, ogrenci.KisiselEpostaOnayliMi,
            program.KaynakAnaDal, program.Tur == OgrenciProgramTuru.Cap ? "Çift Anadal" : "Yan Dal", program.AktifMi,
            await database.KullaniciFotograflari.AnyAsync(f => f.KullaniciId == ogrenci.Id, cancellationToken), gano)
            {
                KabulAkademikYilBaslangici = program.KabulAkademikYilBaslangici, KabulYariyili = program.KabulYariyili,
                Mufredat = mufredatBilgisi?.Mufredat ?? "Atanmamış", DersListesi = mufredatBilgisi?.DersListesi ?? "Atanmamış",
                MufredatAktifMi = mufredatBilgisi?.MufredatAktifMi ?? false,
                MufredatKapsami = mufredatKapsami,
                IlkParolaDavetineUygunMu = koordinator && program.AktifMi && !ogrenci.EmailConfirmed &&
                    !await users.HasPasswordAsync(ogrenci),
                SonKarar = program.SonKarar, KararGerekcesi = program.KararGerekcesi, KararAni = program.KararAni,
                Surum = Convert.ToBase64String(program.RowVersion)
            } });
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    public async Task<IActionResult> OgrenciFotografi(Guid id, CancellationToken cancellationToken)
    {
        var kayit = await OgrenciKaydiniBul(id, cancellationToken);
        if (kayit is null) return NotFound();
        var ogrenciId = kayit.Value.Ogrenci.Id;
        var icerik = await database.KullaniciFotograflari.AsNoTracking().Where(f => f.KullaniciId == ogrenciId)
            .Select(f => f.JpegIcerik).FirstOrDefaultAsync(cancellationToken);
        if (icerik is null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(icerik, "image/jpeg");
    }

    private async Task<(OgrenciProgrami Program, UygulamaKullanicisi Ogrenci)?> OgrenciKaydiniBul(Guid id, CancellationToken cancellationToken)
    {
        var program = await database.OgrenciProgramlari.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id
            && p.HedefBolum == "Bilgisayar Mühendisliği", cancellationToken);
        if (program is null) return null;
        var ogrenci = await database.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == program.OgrenciId, cancellationToken);
        if (ogrenci is null || !await users.IsInRoleAsync(ogrenci, UygulamaRolleri.Ogrenci)) return null;
        return (program, ogrenci);
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Genel dokümanları ve müfredatları açar.
    public async Task<IActionResult> Dokumanlar(CancellationToken cancellationToken)
    {
        var koordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var ogrenci = User.IsInRole(UygulamaRolleri.Ogrenci);
        var model = await PaneliHazirla(koordinator ? "Koordinatör" : ogrenci ? "Öğrenci" : "Bölüm Başkanı",
            koordinator ? KoordinatorBolumleri : ogrenci ? ["Ders Kaydı ve Evrak İşlemleri", "Onay Takip", "Sohbet", "Dokümanlar"] : BaskanBolumleri, ogrenci);
        if (model is null) return Challenge();
        var sablonKayitlari = ogrenci ? new List<DersSablonu>() : await database.DersSablonlari.AsNoTracking()
            .Where(s => s.HedefBolum == "Bilgisayar Mühendisliği" && (koordinator || s.EtkinMi)).OrderByDescending(s => s.EtkinMi).ThenBy(s => s.Ad)
            .ToListAsync(cancellationToken);
        var mufredatlar = await database.Mufredatlar.AsNoTracking().OrderByDescending(x=>x.EtkinMi).ThenByDescending(x=>x.BaslangicYili).ToListAsync(cancellationToken);
        var mufredatSozlugu = mufredatlar.ToDictionary(x=>x.Id);
        var sablonIdleri = sablonKayitlari.Select(s => s.Id).ToArray();
        var dersler = await (from kalem in database.SablonDersleri.AsNoTracking()
            join ders in database.Dersler.AsNoTracking() on kalem.DersId equals ders.Id
            where sablonIdleri.Contains(kalem.SablonId) && kalem.EtkinMi
                && ders.HedefBolum == "Bilgisayar Mühendisliği"
            orderby kalem.MufredatYariyili, kalem.Kod ?? ders.Kod
            select new { kalem.SablonId, kalem.DersId, Kod = kalem.Kod ?? ders.Kod, Ad = kalem.Ad ?? ders.Ad,
                Akts = kalem.Akts ?? ders.Akts, kalem.Kredi, kalem.MufredatYariyili, kalem.Tur }).ToListAsync(cancellationToken);
        var sablonlar = sablonKayitlari.Select(s => new SablonSatirModel(s.Ad,
            s.Tur == OgrenciProgramTuru.Cap ? "Çift Anadal" : "Yan Dal", s.KaynakAnaDal)
        {
            Id = s.Id, Surum = Convert.ToBase64String(s.RowVersion), EtkinMi = s.EtkinMi && (s.MufredatId is not { } groupId || mufredatSozlugu[groupId].EtkinMi),
            OrtakMufredatId = s.MufredatId, MufredatNo = s.MufredatNo, MufredatAdi = s.MufredatId is { } g ? mufredatSozlugu[g].Ad : s.MufredatAdi,
            Dersler = dersler.Where(d => d.SablonId == s.Id)
                .Select(d => new SablonDersSatirModel(d.Kod, d.Ad, d.Akts)
                { DersId = d.DersId, Kredi = d.Kredi, MufredatYariyili = d.MufredatYariyili, Tur = (int)d.Tur }).ToList()
        }).ToList();
        var genelBelgeler = await (from b in database.Belgeler.AsNoTracking()
            join tanim in database.GenelBelgeTanimlari.AsNoTracking() on b.GenelBelgeId equals tanim.Id
            where b.Kapsam == BelgeKapsami.Genel && b.SurumNo != null
            orderby tanim.Tur, b.DosyaAdi
            select new { tanim.Id, tanim.Tur, tanim.RowVersion, BelgeId = b.Id, BelgeSurumu = b.RowVersion, b.GenelArsivdeMi, b.DosyaAdi, b.SurumNo, b.YuklemeAni,
                Mevcut = b.IcerikDurumu == BelgeIcerikDurumu.Mevcut && b.DepolamaAnahtari != null && b.Sha256 != null }).ToListAsync(cancellationToken);
        return View("DokumanlarYeni", model with { AktifBolum = "Dokümanlar", Sablonlar = sablonlar,
            Mufredatlar = mufredatlar.Select(x=>new MufredatSatiri(x.Id,x.Ad,x.BaslangicYili,x.EtkinMi,Convert.ToBase64String(x.RowVersion))).ToList(),
            GenelBelgeler = genelBelgeler.GroupBy(b => b.Id).Select(group => {
                var rows = group.OrderByDescending(b => b.SurumNo).Select(b => new GenelBelgeSatirModel(b.Id, b.Tur,
                    b.DosyaAdi ?? "İçerik bulunamıyor", b.SurumNo!.Value, Convert.ToBase64String(b.RowVersion), b.Mevcut, b.YuklemeAni)
                    { BelgeId = b.BelgeId, BelgeSurumu = Convert.ToBase64String(b.BelgeSurumu), Arsivde = b.GenelArsivdeMi }).ToArray();
                return rows[0] with { Gecmis = rows.Skip(1).ToArray() };
            }).ToList() });
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Akademik şartları ve ders kayıt dönemini açar.
    public async Task<IActionResult> AkademikKosullar([FromServices] TimeProvider clock, int? yil, int? yariyil, CancellationToken ct)
    {
        ViewData["KaynakAnaDallar"] = await database.KaynakAnaDallar.AsNoTracking().OrderBy(x => x.Tur).ThenBy(x => x.Ad).ToListAsync(ct);
        if (!ModelState.IsValid) return BadRequest("Geçerli yıl ve dönem seçin.");
        var currentPeriod = await database.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
        if (currentPeriod is not null)
        {
            var code = AkademikDonemSirasi.Kod(currentPeriod.AkademikYilBaslangici, (int)currentPeriod.Yariyil);
            ViewData["IslemDonemi"] = new IslemDonemiModel(currentPeriod.Id, AkademikDonemSirasi.Ad(code),
                Convert.ToBase64String(currentPeriod.RowVersion), AkademikDonemSirasi.Ad(code + 1));
        }
        var calendarYear = yil ?? TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).Year;
        var semester = yariyil ?? (int?)currentPeriod?.Yariyil ?? 1;
        if (yil is null && currentPeriod is not null) calendarYear = currentPeriod.AkademikYilBaslangici + (semester == 2 ? 1 : 0);
        if (calendarYear is < 2 or > 9998 || semester is < 1 or > 2) return BadRequest("Geçerli yıl ve dönem seçin.");
        var coordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var model = await PaneliHazirla(coordinator ? "Koordinatör" : "Bölüm Başkanı", coordinator ? KoordinatorBolumleri : BaskanBolumleri);
        if (model is null) return Challenge();
        var academicYear = semester == 2 ? calendarYear - 1 : calendarYear;
        var gate = await (from p in database.Donemler.AsNoTracking()
            join g in database.DersKayitKapilari.AsNoTracking() on p.Id equals g.DonemId
            where p.AkademikYilBaslangici == academicYear && (int)p.Yariyil == semester select g).SingleOrDefaultAsync(ct);
        var effective = gate is null ? null : Services.DersKaydi.KayitKapisiKurali.EsikleriUygula(Services.DersKaydi.KayitKapisiServisi.Durum(gate), clock.GetUtcNow());
        var curricula = await database.DersSablonlari.AsNoTracking()
            .Where(t => t.HedefBolum == "Bilgisayar Mühendisliği" && (coordinator || t.EtkinMi))
            .OrderBy(t => t.Tur).ThenBy(t => t.KaynakAnaDal).ThenBy(t => t.MufredatNo)
            .Select(t => new SablonSatirModel(t.Ad, t.Tur == OgrenciProgramTuru.Cap ? "ÇAP" : "Yan Dal", t.KaynakAnaDal)
            { Id = t.Id, Surum = Convert.ToBase64String(t.RowVersion), EtkinMi = t.EtkinMi,
              MufredatNo = t.MufredatNo, MufredatAdi = t.MufredatAdi, OrtakMufredatId = t.MufredatId }).ToListAsync(ct);
        var commonNames = await database.Mufredatlar.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Ad, ct);
        var commonCurricula = await database.Mufredatlar.AsNoTracking().OrderByDescending(x=>x.EtkinMi).ThenByDescending(x=>x.BaslangicYili).ToListAsync(ct);
        curricula = curricula.Select(t => t.OrtakMufredatId is { } commonId && commonNames.TryGetValue(commonId, out var name)
            ? t with { MufredatAdi = name } : t).ToList();
        return View(model with { AktifBolum = "Akademik Koşullar", Sablonlar = curricula,
            Mufredatlar = commonCurricula.Select(x=>new MufredatSatiri(x.Id,x.Ad,x.BaslangicYili,x.EtkinMi,Convert.ToBase64String(x.RowVersion))).ToList(), KayitKapisi = new(calendarYear, semester,
            gate is null ? "" : Convert.ToBase64String(gate.RowVersion), effective?.AcikMi ?? false,
            Services.DersKaydi.KayitZamaniKurali.Goster(effective?.AcilisAniUtc), Services.DersKaydi.KayitZamaniKurali.Goster(effective?.KapanisAniUtc)) });
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci)]
    // Öğrencinin atanmış müfredatını ve ders seçimlerini açar.
    public async Task<IActionResult> DersKaydi([FromServices] Services.DersKaydi.DersListeOkumaServisi reader,
        [FromServices] TimeProvider clock, CancellationToken ct)
    {
        var current = await database.Donemler.AsNoTracking().SingleOrDefaultAsync(x=>x.IslemDonemiMi,ct);
        if(current is null) return BadRequest("Ders kaydı için işlem dönemi belirlenmemiş.");
        return await DersPaneli(reader,clock,true,current.AkademikYilBaslangici+(current.Yariyil==AkademikYariyil.Bahar?1:0),(int)current.Yariyil,ct);
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Not takibi için öğrenci listesini açar.
    public async Task<IActionResult> DersNotTakibi()
    {
        var result = await Ogrenciler(null, null, null);
        if (result is not ViewResult { Model: PanelModel model }) return result;
        return View("DersNotTakibi", model with { AktifBolum = "Not Takibi" });
    }

    [HttpGet, Authorize(Roles=UygulamaRolleri.Ogrenci+","+UygulamaRolleri.Koordinator+","+UygulamaRolleri.BolumBaskani)]
    // Seçilen öğrencinin dönemlik ders ve not geçmişini açar.
    public async Task<IActionResult> DersGecmisi(Guid? programId, CancellationToken ct)
    {
        var actor=await users.GetUserAsync(User); if(actor is null) return Challenge();
        var student=User.IsInRole(UygulamaRolleri.Ogrenci);
        if(programId is null && student) programId=await database.OgrenciProgramlari.Where(x=>x.OgrenciId==actor.Id && x.AktifMi).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(ct);
        if(programId is null || await dersler.IlerlemeOkuAsync(actor.Id,programId.Value,ct) is not { } progress) return Forbid();
        ViewData["DersIlerlemesi"]=progress;
        ViewData["GecmisProgramId"]=programId.Value;
        ViewData["Transkriptler"]=await belgeler.TranskriptlerAsync(actor.Id,programId.Value,ct);
        var model=await PaneliHazirla(student?"Öğrenci":"Koordinatör",student?["Ders Kaydı ve Evrak İşlemleri","Onay Takip","Sohbet","Dokümanlar"]:User.IsInRole(UygulamaRolleri.Koordinator)?KoordinatorBolumleri:BaskanBolumleri,student);
        if(model is null) return Challenge(); ViewData["PanelModel"]=model with {AktifBolum="Not Takibi"};
        var periods=await (from l in database.DersSecimleri.AsNoTracking() join d in database.Donemler.AsNoTracking() on l.DonemId equals d.Id where l.ProgramId==programId orderby d.AkademikYilBaslangici,d.Yariyil select d).ToListAsync(ct);
        ViewData["EvrakDonemAdresleri"] = periods.ToDictionary(p => p.Id, p => Url.Action(nameof(Evrak), new {
            programId, yil = p.AkademikYilBaslangici + (p.Yariyil == AkademikYariyil.Bahar ? 1 : 0), yariyil = (int)p.Yariyil }));
        var rows=new List<KeyValuePair<string,DersPanelListesi>>();
        foreach(var period in periods) {
            var panel=await dersler.OkuAsync(actor.Id,false,period.AkademikYilBaslangici+(period.Yariyil==AkademikYariyil.Bahar?1:0),(int)period.Yariyil,ct);
            if(panel?.Listeler.SingleOrDefault(x=>x.ProgramId==programId) is { } row) rows.Add(new($"{period.AkademikYilBaslangici}–{period.AkademikYilBaslangici+1} {(period.Yariyil==AkademikYariyil.Guz?"Güz":"Bahar")} yarıyılı",row));
        }
        return View(rows);
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
    // Ders listelerini ve onay işlemlerini açar.
    public async Task<IActionResult> OnayTakip([FromServices] Services.DersKaydi.DersListeOkumaServisi reader,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        var period = await database.Donemler.AsNoTracking().SingleOrDefaultAsync(x => x.IslemDonemiMi, ct);
        if (period is null) return BadRequest("Onay Takip için işlem dönemi belirlenmemiş.");
        var calendarYear = period.Yariyil == AkademikYariyil.Bahar ? period.AkademikYilBaslangici + 1 : period.AkademikYilBaslangici;
        return await DersPaneli(reader, clock, false, calendarYear, (int)period.Yariyil, ct);
    }

    [HttpGet,Authorize(Roles=UygulamaRolleri.Ogrenci+","+UygulamaRolleri.Koordinator+","+UygulamaRolleri.BolumBaskani)]
    // Öğrenci evraklarını ve belge isteklerini açar.
    public async Task<IActionResult> Evrak([FromServices] Services.Belgeler.EvrakOkumaServisi files,Guid? programId,int? yil,int? yariyil,string? donem,CancellationToken ct) {
        if(donem is not null) { var parts=donem.Split('-'); if(parts.Length!=2 || !int.TryParse(parts[0],out var selectedYear) || !int.TryParse(parts[1],out var selectedSemester)) return BadRequest("Geçerli dönem seçin."); yil=selectedYear; yariyil=selectedSemester; }
        if(!ModelState.IsValid || programId==Guid.Empty || yil is <2 or >9998 || yariyil is <1 or >2) return BadRequest("Geçerli dönem ve program seçin.");
        bool student=User.IsInRole(UygulamaRolleri.Ogrenci),coordinator=User.IsInRole(UygulamaRolleri.Koordinator);
        var model=await PaneliHazirla(student?"Öğrenci":coordinator?"Koordinatör":"Bölüm Başkanı",
            student?["Ders Kaydı ve Evrak İşlemleri","Onay Takip","Sohbet","Dokümanlar"]:coordinator?KoordinatorBolumleri:BaskanBolumleri,student);
        var actor=await users.GetUserAsync(User); if(model is null || actor is null) return Challenge();
        var data=await files.OkuAsync(actor.Id,programId,null,null,ct:ct,gecmisiEkle:true); if(data is null) return NotFound();
        var firstYear=await database.OgrenciProgramlari.AsNoTracking().Where(p=>p.HedefBolum=="Bilgisayar Mühendisliği" && (!student || p.OgrenciId==actor.Id) && (programId==null || p.Id==programId)).MinAsync(p=>(int?)p.KabulAkademikYilBaslangici,ct);
        var current=await database.Donemler.AsNoTracking().SingleOrDefaultAsync(d=>d.IslemDonemiMi,ct);
        var periods=await database.Donemler.AsNoTracking().Where(d=>current!=null && (firstYear==null || d.AkademikYilBaslangici>=firstYear) &&
            (d.AkademikYilBaslangici<current.AkademikYilBaslangici || d.AkademikYilBaslangici==current.AkademikYilBaslangici && d.Yariyil<=current.Yariyil))
            .OrderByDescending(d=>d.AkademikYilBaslangici).ThenByDescending(d=>d.Yariyil).ToListAsync(ct);
        ViewData["ArsivDonemleri"]=periods.Select(d=>new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem($"{d.AkademikYilBaslangici}–{d.AkademikYilBaslangici+1} {(d.Yariyil==AkademikYariyil.Guz?"Güz":"Bahar")}",$"{d.AkademikYilBaslangici+(d.Yariyil==AkademikYariyil.Bahar?1:0)}-{(int)d.Yariyil}")).ToArray();
        return View(model with {AktifBolum="Öğrenci Evrakları",EvrakPaneli=data});
    }

    private async Task<IActionResult> DersPaneli(Services.DersKaydi.DersListeOkumaServisi reader, TimeProvider clock,
        bool selection, int? yil, int? yariyil, CancellationToken ct)
    {
        var year = yil ?? TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).Year;
        var semester = yariyil ?? 1;
        if (!ModelState.IsValid || year is < 2 or > 9998 || semester is < 1 or > 2) return BadRequest("Geçerli yıl ve dönem seçin.");
        var student = User.IsInRole(UygulamaRolleri.Ogrenci); var coordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var model = await PaneliHazirla(student ? "Öğrenci" : coordinator ? "Koordinatör" : "Bölüm Başkanı",
            student ? ["Ders Kaydı ve Evrak İşlemleri", "Onay Takip", "Sohbet", "Dokümanlar"] : coordinator ? KoordinatorBolumleri : BaskanBolumleri, student);
        var actor = await users.GetUserAsync(User);
        if (model is null || actor is null) return Challenge();
        var panel = await reader.OkuAsync(actor.Id, selection, year, semester, ct);
        if (panel is null) return Forbid();
        if (!selection && !student)
            panel = panel with { Listeler = panel.Listeler.Where(x => x.ProgramAktifMi).ToList() };
        return View("DersKaydi", model with { AktifBolum = selection ? "Ders Kaydı ve Evrak İşlemleri" : "Onay Takip", DersPaneli = panel });
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci + "," + UygulamaRolleri.Koordinator)]
    // Yetkili öğrenci görüşmesini ve koordinatör bilgilerini açar.
    public async Task<IActionResult> Sohbet([FromServices] Services.Sohbet.SohbetServisi service, Guid? ogrenciId, int sayfa = 0, CancellationToken ct = default)
    {
        var student = User.IsInRole(UygulamaRolleri.Ogrenci);
        var model = await PaneliHazirla(student ? "Öğrenci" : "Koordinatör",
            student ? ["Ders Kaydı ve Evrak İşlemleri", "Onay Takip", "Sohbet", "Dokümanlar"] : KoordinatorBolumleri, student);
        var actor = await users.GetUserAsync(User); if (model is null || actor is null) return Challenge();
        if (sayfa is < 0 or > 100000) return BadRequest("Geçerli mesaj sayfası seçin.");
        var data = await service.OkuAsync(actor.Id, ogrenciId, sayfa, ct);
        var meetings = await gorusme.OkuAsync(actor.Id, ct, sohbetAdresi: true);
        return data is null ? Forbid() : View(model with { AktifBolum = "Sohbet", SohbetPaneli = data,
            GorusmeBilgileri = meetings.Where(x => x.Gorev == "Koordinatör").ToList(),
            KendiGorusmeBilgisi = student ? null : meetings.SingleOrDefault(x => x.KullaniciId == actor.Id) });
    }

    private async Task<IActionResult> Goster(string title, IReadOnlyList<string> sections, bool student = false)
    {
        var model = await PaneliHazirla(title, sections, student);
        if (model is not null && student)
        {
            var actor = await users.GetUserAsync(User);
            var programId = await database.OgrenciProgramlari.AsNoTracking().Where(p => p.OgrenciId == actor!.Id &&
                p.AktifMi && p.HedefBolum == "Bilgisayar Mühendisliği").Select(p => p.Id).SingleOrDefaultAsync();
            var display = await ganoEkrani.OkuAsync(actor!.Id, programId);
            ViewData["KosulProgramId"] = programId;
            ViewData["DersIlerlemesi"] = await dersler.IlerlemeOkuAsync(actor.Id, programId);
            ViewData["KosulDonemId"] = await database.Donemler.AsNoTracking().Where(x => x.IslemDonemiMi).Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
            model = model with { GanoGorunumu = display ?? [], DonemIzinleri = await donemIzinleri.OkuAsync(actor.Id, programId), GanoOkumaHatasi = display is null
                ? "GANO değerlendirme geçmişi doğrulanamadı." : null };
        }
        if (model is not null)
        {
            var actor = await users.GetUserAsync(User);
            var meetings = await gorusme.OkuAsync(actor!.Id, sohbetAdresi: true);
            model = model with { GorusmeBilgileri = student ? meetings : [],
                KendiGorusmeBilgisi = student ? null : meetings.SingleOrDefault(x => x.KullaniciId == actor.Id) };
        }
        return model is null ? Challenge() : View("Index", model);
    }

    [HttpGet, Authorize(Roles = UygulamaRolleri.Ogrenci)]
    public async Task<IActionResult> OgrenciDurumu(CancellationToken ct)
    {
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Challenge();
        var programs = database.OgrenciProgramlari.Where(x => x.OgrenciId == actor.Id && x.AktifMi).Select(x => x.Id);
        var lists = await database.DersSecimleri.AsNoTracking().Where(x => programs.Contains(x.ProgramId)).OrderBy(x => x.Id).Select(x => new { x.Id, x.RowVersion }).ToListAsync(ct);
        var ganos = await database.GanoKontrolKayitlari.AsNoTracking().Where(x => programs.Contains(x.ProgramId)).OrderBy(x => x.Id).Select(x => new { x.Id, x.RowVersion }).ToListAsync(ct);
        var grades = await (from a in database.DersDenemeleri.AsNoTracking() join v in database.DersDenemeSurumleri.AsNoTracking() on a.Id equals v.DenemeId
                            where programs.Contains(a.ProgramId) orderby v.Id select v.Id).ToListAsync(ct);
        var documents = await database.Belgeler.AsNoTracking().Where(x => x.ProgramId.HasValue && programs.Contains(x.ProgramId.Value)).OrderBy(x => x.Id).Select(x => new { x.Id, x.RowVersion }).ToListAsync(ct);
        var requests = await database.EkBelgeIstekleri.AsNoTracking().Where(x => programs.Contains(x.ProgramId)).OrderBy(x => x.Id).Select(x => new { x.Id, x.RowVersion }).ToListAsync(ct);
        return Json(new { Listeler = lists.Select(x => new { x.Id, Surum = Convert.ToBase64String(x.RowVersion) }),
            Gano = ganos.Select(x => new { x.Id, Surum = Convert.ToBase64String(x.RowVersion) }), Notlar = grades,
            Belgeler = documents.Select(x => new { x.Id, Surum = Convert.ToBase64String(x.RowVersion) }),
            Istekler = requests.Select(x => new { x.Id, Surum = Convert.ToBase64String(x.RowVersion) }) });
    }

    private async Task<PanelModel?> PaneliHazirla(string title, IReadOnlyList<string> sections, bool student = false)
    {
        var model = await panelModelleri.HazirlaAsync(User, title, sections, student);
        ViewData["PanelBasligi"] = title;
        return model;
    }
}
