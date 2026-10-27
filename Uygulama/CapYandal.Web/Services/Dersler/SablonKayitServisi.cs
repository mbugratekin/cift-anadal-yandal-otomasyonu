using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Models;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.Dersler;

public enum SablonKayitSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, Cakisma, Basarisiz }

public sealed class SablonKayitServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, ILogger<SablonKayitServisi> logger)
{
    // DERS HAVUZU OLUŞTURMA: Tür, kaynak ana dal ve müfredat adı doğrulanır.
    public Task<SablonKayitSonucu> EkleAsync(Guid actorId, SablonEkleModeli model, CancellationToken ct = default)
    {
        var source = model.KaynakAnaDal?.Trim();
        if (model.MufredatNo < 1 || model.Tur is < 1 or > 2 || (model.Tur == 1 && (string.IsNullOrWhiteSpace(source) || source.Length > 128)) ||
            (model.Tur == 2 && !string.IsNullOrWhiteSpace(source)) || !MufredatAdiOku(model.MufredatAdi, out _))
            return Task.FromResult(SablonKayitSonucu.Gecersiz);
        return YonetAsync(actorId, model, null, ct);
    }

    public Task<SablonKayitSonucu> DurumAsync(Guid actorId, SablonDurumModeli model, CancellationToken ct = default)
    {
        if (model.SablonId == Guid.Empty || model.EtkinMi is null || string.IsNullOrEmpty(model.Surum))
            return Task.FromResult(SablonKayitSonucu.Gecersiz);
        return YonetAsync(actorId, null, model, ct);
    }

    public Task<SablonKayitSonucu> AdDegistirAsync(Guid actorId, MufredatAdModeli model, CancellationToken ct = default)
    {
        if (model.SablonId == Guid.Empty || string.IsNullOrEmpty(model.Surum) ||
            !MufredatAdiOku(model.MufredatAdi, out var name) || name is null)
            return Task.FromResult(SablonKayitSonucu.Gecersiz);
        return YonetAsync(actorId, null, new() { SablonId = model.SablonId, Surum = model.Surum }, ct, name);
    }

    private async Task<SablonKayitSonucu> YonetAsync(Guid actorId, SablonEkleModeli? create, SablonDurumModeli? state, CancellationToken ct, string? newName = null)
    {
        if (db.ChangeTracker.HasChanges()) return SablonKayitSonucu.Basarisiz;
        try
        {
            if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return SablonKayitSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return SablonKayitSonucu.IzinYok;
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                    return SablonKayitSonucu.IzinYok;
                DersSablonu template;
                string before;
                string operation;
                if (create is not null)
                {
                    var source = create.Tur == 1 ? create.KaynakAnaDal!.Trim() : null;
                    var kind = (OgrenciProgramTuru)create.Tur;
                    if (await db.DersSablonlari.AnyAsync(t => t.Tur == kind && t.HedefBolum == "Bilgisayar Mühendisliği" && t.KaynakAnaDal == source && t.MufredatNo == create.MufredatNo, ct))
                        return SablonKayitSonucu.Cakisma;
                    MufredatAdiOku(create.MufredatAdi, out var curriculumName);
                    if (curriculumName is not null && await db.DersSablonlari.AnyAsync(t => t.Tur == kind && t.HedefBolum == "Bilgisayar Mühendisliği" && t.KaynakAnaDal == source && t.MufredatAdi == curriculumName, ct))
                        return SablonKayitSonucu.Cakisma;
                    template = new() { Id = Guid.NewGuid(), Tur = kind, HedefBolum = "Bilgisayar Mühendisliği", KaynakAnaDal = source,
                        Ad = source ?? "Bilgisayar Mühendisliği", MufredatNo = create.MufredatNo, MufredatAdi = curriculumName,
                        OlusturanKullaniciId = actorId, OlusturulmaAni = DateTimeOffset.UtcNow };
                    db.DersSablonlari.Add(template);
                    before = "null"; operation = "SablonEkle";
                }
                else
                {
                    var found = await db.DersSablonlari.SingleOrDefaultAsync(t => t.Id == state!.SablonId, ct);
                    if (found is null) return SablonKayitSonucu.Bulunamadi;
                    template = found; await db.Entry(template).ReloadAsync(ct);
                    if (template.MufredatId is not null) return SablonKayitSonucu.Gecersiz; // Ortak ad/durum yalnız ortak yönetimden değişir.
                    if (template.HedefBolum != "Bilgisayar Mühendisliği") return SablonKayitSonucu.Bulunamadi;
                    byte[] version;
                    try { version = Convert.FromBase64String(state!.Surum); }
                    catch (FormatException) { return SablonKayitSonucu.Cakisma; }
                    if (version.Length != 8 || !template.RowVersion.SequenceEqual(version)) return SablonKayitSonucu.Cakisma;
                    if (newName is not null)
                    {
                        if (template.MufredatAdi == newName) { completed = true; if (own is not null) await tx.CommitAsync(ct); return SablonKayitSonucu.Basarili; }
                        if (await db.DersSablonlari.AnyAsync(t => t.Id != template.Id && t.Tur == template.Tur &&
                            t.HedefBolum == template.HedefBolum && t.KaynakAnaDal == template.KaynakAnaDal && t.MufredatAdi == newName, ct))
                            return SablonKayitSonucu.Cakisma;
                        before = JsonSerializer.Serialize(TemplateSnapshot(template));
                        template.MufredatAdi = newName;
                        operation = "MufredatAdiGuncelle";
                    }
                    else
                    {
                    var enabled = state!.EtkinMi!.Value;
                    if (template.EtkinMi == enabled) { completed = true; if (own is not null) await tx.CommitAsync(ct); return SablonKayitSonucu.Basarili; }
                    before = JsonSerializer.Serialize(TemplateSnapshot(template));
                    template.EtkinMi = enabled;
                    operation = enabled ? "SablonEtkinlestir" : "SablonPasiflestir";
                    // Geçmiş dönem açıklıkları yeniden etkinleştirmede kendiliğinden canlanmaz.
                    var openings = await db.SablonDonemDersleri.Where(o => o.SablonId == template.Id && o.SecimeAcikMi).ToListAsync(ct);
                    foreach (var opening in openings)
                    {
                        var previous = JsonSerializer.Serialize(new { opening.SecimeAcikMi, opening.OgretimElemani });
                        opening.SecimeAcikMi = false; opening.DegistirenKullaniciId = actorId; opening.DegisiklikAni = DateTimeOffset.UtcNow;
                        Audit(nameof(SablonDonemDersi), $"{template.Id}/{opening.DonemId}/{opening.DersId}", previous,
                            JsonSerializer.Serialize(new { opening.SecimeAcikMi, opening.OgretimElemani }), actorId, operation);
                    }
                    }
                }
                Audit(nameof(DersSablonu), template.Id.ToString(), before, JsonSerializer.Serialize(TemplateSnapshot(template)), actorId, operation);
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true; return SablonKayitSonucu.Basarili;
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
        catch (DbUpdateException) { return SablonKayitSonucu.Cakisma; }
        catch (Exception) { logger.LogWarning("Şablon yönetimi tamamlanamadı."); return SablonKayitSonucu.Basarisiz; }
    }

        private static object TemplateSnapshot(DersSablonu t) => new { t.Id, t.Ad, t.MufredatNo, t.MufredatAdi, t.Tur, t.HedefBolum, t.KaynakAnaDal, t.EtkinMi, t.OlusturanKullaniciId, t.OlusturulmaAni };
    public static bool MufredatAdiOku(string? text, out string? name)
    {
        name = null;
        if (text is null) return true;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return true;
        if (trimmed.Length > 100) return false;
        name = Regex.Replace(trimmed, @"\s+", " ");
        return true;
    }
    public static bool AktsOku(string? text, out decimal value)
    {
        value = 0;
        var normalized = text?.Trim().Replace(',', '.') ?? "";
        return Regex.IsMatch(normalized, @"^[0-9]{1,4}(\.[0-9]{1,2})?$", RegexOptions.CultureInvariant)
            && decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    public static bool KrediOku(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!AktsOku(text, out var parsed)) return false;
        value = parsed;
        return true;
    }

    public static bool GirdiGecerli(SablonKaydetModeli model)
    {
        if (model.SablonId == Guid.Empty || model.Dersler is null || model.Dersler.Count > 500) return false;
        if (model.MufredatAdi is not null && !MufredatAdiOku(model.MufredatAdi, out _)) return false;
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in model.Dersler)
        {
            if (item.Cikar) { if (item.DersId is null || item.DersId == Guid.Empty) return false; continue; }
            if (string.IsNullOrWhiteSpace(item.Kod) || item.Kod.Trim().Length != 7 ||
                item.Kod.Trim().Any(character => character is < '0' or > '9') ||
                string.IsNullOrWhiteSpace(item.Ad) || item.Ad.Trim().Length > 250 ||
                !AktsOku(item.Akts, out _) || !KrediOku(item.Kredi, out _) || item.MufredatYariyili is < 1 or > 8 || item.Tur is < 1 or > 3 ||
                !codes.Add(item.Kod.Trim())) return false;
        }
        return true;
    }

    // MÜFREDAT DERSLERİNİ KAYDETME: Girdiler, yetki ve sürüm denetlenerek havuz güncellenir.
    public async Task<SablonKayitSonucu> KaydetAsync(Guid actorId, SablonKaydetModeli model, CancellationToken ct = default)
    {
        if (!GirdiGecerli(model)) return SablonKayitSonucu.Gecersiz;
        try
        {
            if (db.Database.CurrentTransaction is { } caller && caller.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return SablonKayitSonucu.Basarisiz;
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!;
            var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            var completed = false;
            try
            {
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return SablonKayitSonucu.IzinYok;
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(UygulamaRolleri.Koordinator))
                    return SablonKayitSonucu.IzinYok;
                var template = await db.DersSablonlari.SingleOrDefaultAsync(s => s.Id == model.SablonId, ct);
                if (template is null) return SablonKayitSonucu.Bulunamadi;
                await db.Entry(template).ReloadAsync(ct);
                if (!template.EtkinMi || template.HedefBolum != "Bilgisayar Mühendisliği" ||
                    template.Tur is not (OgrenciProgramTuru.Cap or OgrenciProgramTuru.YanDal)) return SablonKayitSonucu.Bulunamadi;
                byte[] version;
                try { version = Convert.FromBase64String(model.Surum); }
                catch (FormatException) { return SablonKayitSonucu.Cakisma; }
                if (version.Length != 8 || !template.RowVersion.SequenceEqual(version)) return SablonKayitSonucu.Cakisma;

                var rows = await db.SablonDersleri.Where(r => r.SablonId == template.Id).ToListAsync(ct);
                foreach (var row in rows) await db.Entry(row).ReloadAsync(ct);
                var ids = rows.Select(r => r.DersId).ToArray();
                var catalog = await db.Dersler.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
                foreach (var row in rows)
                {
                    if (!catalog.TryGetValue(row.DersId, out var course) || course.Tur != template.Tur ||
                        course.HedefBolum != template.HedefBolum || course.KaynakAnaDal != template.KaynakAnaDal)
                        return SablonKayitSonucu.Cakisma;
                    row.Kod ??= course.Kod; row.Ad ??= course.Ad; row.Akts ??= course.Akts;
                }
                var active = rows.Where(r => r.EtkinMi).Select(r => r.DersId).ToHashSet();
                var suppliedIds = model.Dersler.Where(r => r.DersId is not null).Select(r => r.DersId!.Value).ToList();
                // Eksik/truncated form sessiz ders silme sayilmaz.
                if (suppliedIds.Distinct().Count() != suppliedIds.Count || !active.SetEquals(suppliedIds)) return SablonKayitSonucu.Cakisma;
                var before = JsonSerializer.Serialize(rows.Select(Snapshot));
                // İki ders kodu yer değiştirirken geçici çakışma oluşması engellenir.
                foreach (var row in rows.Where(r => r.EtkinMi)) row.EtkinMi = false;
                db.Entry(template).Property(t => t.Ad).IsModified = true;
                await db.SaveChangesAsync(ct);
                var used = new HashSet<Guid>();
                var removed = model.Dersler.Where(r => r.Cikar).Select(r => r.DersId!.Value).ToHashSet();
                foreach (var input in model.Dersler.Where(r => !r.Cikar))
                {
                    SablonDersi? row;
                    if (input.DersId is { } id) row = rows.Single(r => r.DersId == id);
                    else
                    {
                        var code = input.Kod!.Trim();
                        var course = await db.Dersler.SingleOrDefaultAsync(d => d.Tur == template.Tur &&
                            d.HedefBolum == template.HedefBolum && d.KaynakAnaDal == template.KaynakAnaDal && d.Kod == code, ct);
                        if (course is null)
                        {
                            AktsOku(input.Akts, out var credits);
                            course = new() { Id = Guid.NewGuid(), Tur = template.Tur, HedefBolum = template.HedefBolum,
                                KaynakAnaDal = template.KaynakAnaDal, Kod = code, Ad = input.Ad!.Trim(), Akts = credits };
                            db.Dersler.Add(course);
                        }
                        row = rows.SingleOrDefault(r => r.DersId == course.Id);
                        if (row is not null && active.Contains(row.DersId) && !removed.Contains(row.DersId)) return SablonKayitSonucu.Gecersiz;
                        if (row is null)
                        {
                            row = new() { SablonId = template.Id, DersId = course.Id };
                            rows.Add(row); db.SablonDersleri.Add(row);
                        }
                        else
                        {
                            // Geri ekleme eski donem acilislarini kendiliginden canlandirmasin.
                            var openings = await db.SablonDonemDersleri.Where(o => o.SablonId == template.Id && o.DersId == row.DersId && o.SecimeAcikMi).ToListAsync(ct);
                            foreach (var opening in openings)
                            {
                                var old = JsonSerializer.Serialize(new { opening.SecimeAcikMi, opening.OgretimElemani });
                                opening.SecimeAcikMi = false; opening.DegistirenKullaniciId = actorId; opening.DegisiklikAni = DateTimeOffset.UtcNow;
                                Audit(nameof(SablonDonemDersi), $"{template.Id}/{opening.DonemId}/{row.DersId}", old,
                                    JsonSerializer.Serialize(new { opening.SecimeAcikMi, opening.OgretimElemani }), actorId);
                            }
                        }
                    }
                    if (!used.Add(row.DersId)) return SablonKayitSonucu.Gecersiz;
                    AktsOku(input.Akts, out var akts);
                    KrediOku(input.Kredi, out var kredi);
                    row.Kod = input.Kod!.Trim(); row.Ad = input.Ad!.Trim(); row.Akts = akts;
                    row.Kredi = kredi;
                    row.MufredatYariyili = input.MufredatYariyili; row.Tur = (DersTuru)input.Tur; row.EtkinMi = true;
                }
                if (model.MufredatAdiGonderildi)
                {
                    MufredatAdiOku(model.MufredatAdi, out var targetName);
                    if (template.MufredatId is not null && template.MufredatAdi != targetName)
                        return SablonKayitSonucu.Gecersiz;
                    if (template.MufredatAdi != targetName)
                    {
                        if (targetName is not null && await db.DersSablonlari.AnyAsync(t => t.Id != template.Id && t.Tur == template.Tur && t.HedefBolum == template.HedefBolum && t.KaynakAnaDal == template.KaynakAnaDal && t.MufredatAdi == targetName, ct))
                            return SablonKayitSonucu.Cakisma;
                        var previousName = template.MufredatAdi;
                        template.MufredatAdi = targetName;
                        db.Entry(template).Property(t => t.MufredatAdi).IsModified = true;
                        Audit(nameof(DersSablonu), template.Id.ToString(),
                            JsonSerializer.Serialize(new { MufredatAdi = previousName }),
                            JsonSerializer.Serialize(new { MufredatAdi = targetName }),
                            actorId, "MufredatAdiGuncelle");
                    }
                }
                Audit(nameof(DersSablonu), template.Id.ToString(), before, JsonSerializer.Serialize(rows.Select(Snapshot)), actorId);
                await db.SaveChangesAsync(ct);
                if (own is not null) await tx.CommitAsync(ct);
                completed = true;
                return SablonKayitSonucu.Basarili;
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
        catch (DbUpdateException) { return SablonKayitSonucu.Cakisma; }
        catch (Exception) { logger.LogWarning("Şablon kaydı tamamlanamadı."); return SablonKayitSonucu.Basarisiz; }
    }

    private static object Snapshot(SablonDersi row) => new { row.DersId, row.Kod, row.Ad, row.Kredi, row.Akts, row.MufredatYariyili, row.Tur, row.EtkinMi };
    private void Audit(string type, string key, string before, string after, Guid actor, string operation = "SablonDuzenle") => db.DegisiklikGecmisi.Add(new()
    {
        Id = Guid.NewGuid(), VarlikTuru = type, VarlikAnahtari = key, Islem = operation,
        OncekiDeger = before, YeniDeger = after, YapanKullaniciId = actor, IslemAni = DateTimeOffset.UtcNow
    });
}
