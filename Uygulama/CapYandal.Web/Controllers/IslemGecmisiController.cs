using CapYandal.Web.Data;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Denetim;
using CapYandal.Web.Services.Paneller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapYandal.Web.Controllers;

[Authorize(Roles = UygulamaRolleri.Koordinator + "," + UygulamaRolleri.BolumBaskani)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IslemGecmisiController(AppDbContext db, PanelModelServisi panels) : Controller
{
    // İşlem geçmişi: salt okunur ve sınırlı sayfalama ile eski/yeni değerler gösterilir.
    [HttpGet]
    public async Task<IActionResult> Index(int sayfa = 0, CancellationToken ct = default, int? sayfaNo = null)
    {
        if (sayfaNo is { } numara)
        {
            if (numara < 1) return BadRequest("Geçerli bir geçmiş sayfası seçin.");
            sayfa = numara - 1;
        }
        if (sayfa < 0 || sayfa > int.MaxValue / DenetimSayfaModeli.SayfaBoyutu) return BadRequest("Geçerli bir geçmiş sayfası seçin.");
        var coordinator = User.IsInRole(UygulamaRolleri.Koordinator);
        var model = await panels.HazirlaAsync(User, coordinator ? "Koordinatör" : "Bölüm Başkanı",
            coordinator ? PanelBolumleri.Koordinator : PanelBolumleri.Baskan, ct: ct);
        if (model is null) return Challenge();
        var query = from row in db.DegisiklikGecmisi.AsNoTracking()
            join actor in db.Users.AsNoTracking() on row.YapanKullaniciId equals (Guid?)actor.Id into actors
            from user in actors.DefaultIfEmpty()
            orderby row.IslemAni descending, row.Id descending
            select new { Kayit = row, Yapan = user == null ? "Kimlik doğrulanmamış" : ((user.Ad ?? "") + " " + (user.Soyad ?? "")).Trim() };
        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)DenetimSayfaModeli.SayfaBoyutu));
        if (sayfa >= pages) return BadRequest("Geçerli bir geçmiş sayfası seçin.");
        var rawRows = await query.Skip(sayfa * DenetimSayfaModeli.SayfaBoyutu).Take(DenetimSayfaModeli.SayfaBoyutu).ToListAsync(ct);

        var actorIds = rawRows.Where(x => x.Kayit.YapanKullaniciId.HasValue).Select(x => x.Kayit.YapanKullaniciId!.Value).Distinct().ToArray();
        var actorRoles = (await (from link in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on link.RoleId equals role.Id
            where actorIds.Contains(link.UserId)
            select new { link.UserId, role.Name }).ToListAsync(ct))
            .GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => string.Join(", ", x.Select(r => RolAdi(r.Name)).Distinct()));

        // Teknik anahtar doğrudan program kimliği değilse ilişkili kayıttan öğrenci programına ulaşılır.
        var keys = rawRows.Select(x => Guid.TryParse(x.Kayit.VarlikAnahtari, out var id) ? id : (Guid?)null).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var keyPrograms = new Dictionary<Guid, Guid>();
        foreach (var item in await db.DersSecimleri.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, x.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await db.DersDenemeleri.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, x.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await db.ProgramTakipNotlari.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, x.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await db.ProgramDonemIzinleri.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, x.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await db.Belgeler.AsNoTracking().Where(x => keys.Contains(x.Id) && x.ProgramId != null).Select(x => new { x.Id, ProgramId = x.ProgramId!.Value }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await db.EkBelgeIstekleri.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, x.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;
        foreach (var item in await (from request in db.DersDuzeltmeTalepleri.AsNoTracking()
            join list in db.DersSecimleri.AsNoTracking() on request.ListeId equals list.Id
            where keys.Contains(request.Id) select new { request.Id, list.ProgramId }).ToListAsync(ct)) keyPrograms[item.Id] = item.ProgramId;

        var rowPrograms = rawRows.ToDictionary(x => x.Kayit.Id, x =>
            DenetimGorunumu.ProgramKimligi(x.Kayit.VarlikTuru, x.Kayit.VarlikAnahtari, x.Kayit.OncekiDeger, x.Kayit.YeniDeger)
            ?? (Guid.TryParse(x.Kayit.VarlikAnahtari, out var key) && keyPrograms.TryGetValue(key, out var programId) ? programId : (Guid?)null));
        var programIds = rowPrograms.Values.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var programs = await (from program in db.OgrenciProgramlari.AsNoTracking()
            join student in db.Users.AsNoTracking() on program.OgrenciId equals student.Id
            where programIds.Contains(program.Id)
            select new { program.Id, student.OgrenciNo, Ad = ((student.Ad ?? "") + " " + (student.Soyad ?? "")).Trim() }).ToDictionaryAsync(x => x.Id, ct);

        var rows = rawRows.Select(x =>
        {
            var change = DenetimGorunumu.DegisiklikleriOku(x.Kayit.OncekiDeger, x.Kayit.YeniDeger, x.Kayit.VarlikTuru);
            var entity = DenetimGorunumu.VarlikAdi(x.Kayit.VarlikTuru);
            var target = rowPrograms[x.Kayit.Id] is { } programId && programs.TryGetValue(programId, out var student)
                ? $"{student.OgrenciNo} {student.Ad} — {entity}" : entity;
            var role = x.Kayit.YapanKullaniciId is { } actor && actorRoles.TryGetValue(actor, out var roleName) ? roleName : "Sistem";
            return new DenetimSatirModeli(x.Kayit.IslemAni, x.Yapan, role, target, DenetimGorunumu.IslemAdi(x.Kayit.Islem),
                change.Gerekce, change.Degisiklikler, x.Kayit.VarlikTuru, x.Kayit.VarlikAnahtari, x.Kayit.Islem,
                x.Kayit.OncekiDeger, x.Kayit.YeniDeger);
        }).ToList();
        ViewData["PanelBasligi"] = model.Baslik;
        return View(model with { AktifBolum = "İşlem Geçmişi", DenetimGecmisi = new(sayfa, sayfa + 1 < pages, rows) { ToplamKayit = total } });
    }

    private static string RolAdi(string? role) => role switch
    {
        UygulamaRolleri.Koordinator => "Koordinatör",
        UygulamaRolleri.BolumBaskani => "Bölüm Başkanı",
        UygulamaRolleri.Ogrenci => "Öğrenci",
        _ => role ?? "Sistem"
    };
}
