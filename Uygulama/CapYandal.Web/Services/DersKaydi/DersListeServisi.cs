using System.Data;
using System.Text.Json;
using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapYandal.Web.Services.DersKaydi;

public enum DersListeSonucu { Basarili, Gecersiz, IzinYok, Bulunamadi, KayitKapali, Cakisma, Basarisiz, DonemIzinli }
public sealed record DersListeCevabi(DersListeSonucu Sonuc, Guid? ListeId = null);
public sealed record DersGonderGirdisi(Guid ProgramId, Guid DonemId, string? SablonSurum, Guid[]? Dersler, string? ListeSurum = null);
public sealed record DersDuzeltGirdisi(Guid ListeId, string? ListeSurum, string? SablonSurum, Guid[]? Dersler, string? Gerekce = null);

public sealed class DersListeServisi(AppDbContext db, UserManager<UygulamaKullanicisi> users,
    HesapErisimi access, TimeProvider clock, ILogger<DersListeServisi> logger)
{
    public Task<DersListeCevabi> GonderAsync(Guid actor, DersGonderGirdisi input, CancellationToken ct = default) =>
        IslemAsync(actor, input.ProgramId, input.DonemId, null, input.ListeSurum, input.SablonSurum, input.Dersler, null, ct);
    public Task<DersListeCevabi> DuzeltAsync(Guid actor, DersDuzeltGirdisi input, CancellationToken ct = default) =>
        IslemAsync(actor, Guid.Empty, Guid.Empty, input.ListeId, input.ListeSurum, input.SablonSurum, input.Dersler, input.Gerekce, ct);

    internal Task<DersListeCevabi> MazeretliAsync(Guid actor, Guid program, Guid period, Guid? list,
        string? version, string? templateVersion, Guid[] courses, string reason, CancellationToken ct) =>
        IslemAsync(actor, program, period, list, version, templateVersion, courses, reason, ct, true);

    private async Task<DersListeCevabi> IslemAsync(Guid actorId, Guid programId, Guid periodId, Guid? listId,
        string? listVersion, string? templateVersion, Guid[]? requested, string? reason, CancellationToken ct, bool mazeretli = false)
    {
        static DersListeCevabi Result(DersListeSonucu status) => new(status);
        bool correction = listId is not null;
        if (actorId == Guid.Empty || requested is null || (!correction && requested.Length == 0) || requested.Length > 1000 ||
            requested.Any(id => id == Guid.Empty) || requested.Distinct().Count() != requested.Length ||
            (!correction && (programId == Guid.Empty || periodId == Guid.Empty)) || listId == Guid.Empty ||
            (correction && (listVersion is not { Length: 12 } || string.IsNullOrWhiteSpace(reason) || reason.Length > 2000))) return Result(DersListeSonucu.Gecersiz);
        reason = reason?.Trim();
        if (mazeretli && (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000 || requested.Length == 0)) return Result(DersListeSonucu.Gecersiz);
        if (db.ChangeTracker.HasChanges()) return Result(DersListeSonucu.Basarisiz);
        var selected = requested.ToHashSet(); var written = new List<object>(); bool complete = false;
        try
        {
            if (db.Database.CurrentTransaction is { } ext && ext.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return Result(DersListeSonucu.Basarisiz);
            // Yalnız adresleme; bağlar daha sonra kilitli listeden doğrulanır.
            if (correction)
            {
                var links = await db.DersSecimleri.FromSqlInterpolated($"SELECT * FROM [DersSecimleri] WITH (READUNCOMMITTED) WHERE [Id] = {listId}")
                    .AsNoTracking().Select(l => new { l.ProgramId, l.DonemId }).SingleOrDefaultAsync(ct);
                if (links is null) return Result(DersListeSonucu.Bulunamadi);
                programId = links.ProgramId; periodId = links.DonemId;
            }
            await using var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var tx = own ?? db.Database.CurrentTransaction!; var point = Guid.NewGuid().ToString("N");
            if (own is null) await tx.CreateSavepointAsync(point, ct);
            try
            {
                var period = await db.Donemler.FromSqlInterpolated($"SELECT * FROM [Donemler] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {periodId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (period is null) return Result(DersListeSonucu.Bulunamadi);
                DersKayitKapisi? gate = null;
                gate = await db.DersKayitKapilari.FromSqlInterpolated($"SELECT * FROM [DersKayitKapilari] WITH (UPDLOCK,HOLDLOCK) WHERE [DonemId] = {periodId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                var program = await db.OgrenciProgramlari.FromSqlInterpolated($"SELECT * FROM [OgrenciProgramlari] WITH (UPDLOCK,HOLDLOCK) WHERE [Id] = {programId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                var actor = await users.FindByIdAsync(actorId.ToString());
                if (actor is null) return Result(DersListeSonucu.IzinYok);
                await db.Entry(actor).ReloadAsync(ct);
                var permission = await access.OkuAsync(actor);
                var role = correction || mazeretli ? UygulamaRolleri.Koordinator : UygulamaRolleri.Ogrenci;
                if (!permission.Izinli || permission.ParolaDegisikligiGerekli || !permission.Roller.Contains(role) ||
                    program?.HedefBolum != "Bilgisayar Mühendisliği" || (!correction && !mazeretli && (program.OgrenciId != actorId || !program.AktifMi)))
                    return Result(DersListeSonucu.IzinYok);
                if (!mazeretli && !Acik(gate)) return Result(DersListeSonucu.KayitKapali);
                if (mazeretli && (!program.AktifMi || !period.IslemDonemiMi || Acik(gate))) return Result(DersListeSonucu.Gecersiz);
                if (!correction || mazeretli)
                {
                    var periodCode = period.AkademikYilBaslangici * 2 + (int)period.Yariyil - 1;
                    if (await db.ProgramDonemIzinleri.AnyAsync(x => x.ProgramId == programId && x.IptalAni == null &&
                        x.Baslangic <= periodCode && periodCode <= x.Bitis, ct)) return Result(DersListeSonucu.DonemIzinli);
                }
                var list = await db.DersSecimleri.FromSqlInterpolated($"SELECT * FROM [DersSecimleri] WITH (UPDLOCK,HOLDLOCK) WHERE [ProgramId] = {programId} AND [DonemId] = {periodId}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (!correction && !mazeretli && list is not null &&
                    (list.Durum != DersSecimiDurumu.Reddedildi || !OnayGecisKurali.OgrenciGonderebilir((KayitOnayDurumu)(int)list.Durum, list.OgrenciKilidi) ||
                     !KayitSurumKurali.KomutGuncel(list.Id, list.Id, list.RowVersion, listVersion))) return Result(DersListeSonucu.Cakisma);
                if (mazeretli && list is not null && !correction) return Result(DersListeSonucu.Cakisma);
                if (correction && (list is null || list.Id != listId || !KayitSurumKurali.KomutGuncel(list.Id, listId!.Value, list.RowVersion, listVersion)))
                    return Result(DersListeSonucu.Cakisma);
                if (correction && (!program.AktifMi || list!.Durum == DersSecimiDurumu.IptalEdildi || !mazeretli && list.Durum == DersSecimiDurumu.Onaylandi))
                    return Result(DersListeSonucu.Gecersiz); // Önce gerekçeli açık karar ile kilitli karardan çıkılmalıdır.
                var old = list is null ? new List<DersSecimKalemi>() : await db.DersSecimKalemleri
                    .FromSqlInterpolated($"SELECT * FROM [DersSecimKalemleri] WITH (UPDLOCK,HOLDLOCK) WHERE [ListeId] = {list.Id}").AsNoTracking().ToListAsync(ct);
                var active = old.Where(k => k.IptalAni is null).ToArray();
                // DERS DEĞİŞİKLİĞİ: Yeni seçilenlerle mevcut kalemlerin farkı bulunur; korunacak dersler aşağıda denetlenir.
                var addedIds = selected.Except(active.Select(k => k.DersId)).ToArray();
                var removed = active.Where(k => !selected.Contains(k.DersId)).ToArray();
                var requests = list is null ? new List<DersDuzeltmeTalebi>() : await db.DersDuzeltmeTalepleri.FromSqlInterpolated($"SELECT * FROM [DersDuzeltmeTalepleri] WITH (UPDLOCK,HOLDLOCK) WHERE [ListeId]={list.Id} AND [KapatilmaAni] IS NULL").AsNoTracking().ToListAsync(ct);
                if (!correction && requests.Count > 0)
                {
                    var rejected = active.Where(k => requests.Any(r => r.KalemId == k.Id)).Select(k => k.DersId).ToHashSet();
                    if (rejected.Count != requests.Count || addedIds.Length != rejected.Count || selected.Overlaps(rejected) || active.Any(k => !rejected.Contains(k.DersId) && !selected.Contains(k.DersId)))
                        return Result(DersListeSonucu.Gecersiz);
                }
                if (correction && !mazeretli && addedIds.Length == 0 && removed.Length == 0)
                {
                    if (own is not null) await own.CommitAsync(ct);
                    complete = true; return new(DersListeSonucu.Basarili, list!.Id); // Kopyalar ve onay değişmez.
                }
                var newCopies = new List<(DersSnapshot Copy, string? Teacher)>();
                Guid templateId;
                if (list is not null) templateId = list.SablonId;
                else if (program.MufredatSablonId is { } assigned) templateId = assigned;
                else return Result(DersListeSonucu.Gecersiz);
                // DOĞRU HAVUZ: Yeni dersler yalnız programın atanmış şablonundan, sunucunun okuduğu değerlerle alınır.
                if (addedIds.Length > 0)
                {
                    if (program.MufredatSablonId != templateId) return Result(DersListeSonucu.Cakisma);
                    // XLOCK, şablon kaydının parent okumasıyla da çatışır; parçalı şablon kopyalanmaz.
                    var template = await db.DersSablonlari.FromSqlInterpolated($"SELECT * FROM [DersSablonlari] WITH (XLOCK,HOLDLOCK) WHERE [Id] = {templateId} AND [Tur] = {program.Tur} AND [HedefBolum] = {program.HedefBolum} AND (([Tur] = 2 AND [KaynakAnaDal] IS NULL) OR ([Tur] = 1 AND [KaynakAnaDal] = {program.KaynakAnaDal}))")
                        .AsNoTracking().SingleOrDefaultAsync(ct);
                    if (template is null || !template.EtkinMi) return Result(DersListeSonucu.Gecersiz);
                    if (!KayitSurumKurali.KomutGuncel(template.Id, templateId, template.RowVersion, templateVersion)) return Result(DersListeSonucu.Cakisma);
                    var courses = await db.SablonDersleri.AsNoTracking().Where(c => c.SablonId == templateId && addedIds.Contains(c.DersId)).ToListAsync(ct);
                    if (courses.Count != addedIds.Length) return Result(DersListeSonucu.Gecersiz);
                    // Öğretim elemanı varsa kopyalanır; dönem açılışının varlığı/açıklığı şart değildir.
                    var teachers = await db.SablonDonemDersleri.AsNoTracking().Where(c => c.SablonId == templateId && c.DonemId == periodId && addedIds.Contains(c.DersId))
                        .ToDictionaryAsync(c => c.DersId, c => c.OgretimElemani, ct);
                    foreach (var course in courses.OrderBy(c => c.MufredatYariyili).ThenBy(c => c.Kod))
                    {
                        if (!DersSnapshotKurali.Olustur(course, templateId, out var copy)) return Result(DersListeSonucu.Gecersiz);
                        newCopies.Add((copy!, teachers.GetValueOrDefault(course.DersId)));
                    }
                }
                // Arka plan işi gecikse bile kapanış sınırı sunucu saatinden tekrar çözülür.
                if (!mazeretli && !Acik(gate)) return Result(DersListeSonucu.KayitKapali);
                var now = clock.GetUtcNow(); var previous = list is not null ? JsonSerializer.Serialize(new { Liste = list, Kalemler = old }) : null;
                foreach (var request in requests)
                {
                    db.Attach(request); request.KapatilmaAni = now; request.KapatanKullaniciId = actorId; written.Add(request);
                }
                if (list is not null && await DersListeSurumKaydi.EskiyiKoruAsync(db, list, old, actorId, now, ct) is { } oldCopy) written.Add(oldCopy);
                if (list is null)
                {
                    list = new() { Id = Guid.NewGuid(), ProgramId = programId, DonemId = periodId, SablonId = templateId, GonderenKullaniciId = actorId, GonderimAni = now };
                    db.DersSecimleri.Add(list);
                }
                else
                {
                    TakipTemizle<DersSecimi>(list.Id); db.Attach(list);
                    list.OgrenciKilidi |= list.Durum is DersSecimiDurumu.Onaylandi or DersSecimiDurumu.IptalEdildi;
                    list.Durum = DersSecimiDurumu.OnayBekliyor; list.OnaylayanKullaniciId = null; list.OnayAni = null; list.KararGerekcesi = null;
                    if (!correction) { list.GonderenKullaniciId = actorId; list.GonderimAni = now; }
                    db.Entry(list).Property(l => l.Durum).IsModified = true; // Sadece kalem değişse de üst sürüm ilerler.
                }
                if (mazeretli) { list.MazeretliMi = true; list.OgrenciKilidi = true; }
                written.Add(list);
                foreach (var item in removed)
                {
                    TakipTemizle<DersSecimKalemi>(item.Id); db.Attach(item);
                    item.IptalAni = now; item.IptalEdenKullaniciId = actorId; item.IptalGerekcesi = reason ?? "Onay öncesi öğrenci güncellemesi"; written.Add(item);
                }
                var newItems = new List<DersSecimKalemi>();
                foreach (var (copy, teacher) in newCopies)
                {
                    var item = new DersSecimKalemi { Id = Guid.NewGuid(), ListeId = list.Id, ProgramId = programId, DonemId = periodId, SablonId = templateId, DersId = copy.DersId,
                        DersKoduKopyasi = copy.Kod, DersAdiKopyasi = copy.Ad, AktsKopyasi = copy.Akts, KrediKopyasi = copy.Kredi, TurKopyasi = copy.Tur,
                        MufredatYariyiliKopyasi = copy.MufredatYariyili, OgretimElemaniKopyasi = teacher, EkleyenKullaniciId = actorId, EklenmeAni = now,
                        DuzeltenKullaniciId = correction ? actorId : null, DuzeltmeAni = correction ? now : null, DuzeltmeGerekcesi = correction ? reason : null };
                    db.DersSecimKalemleri.Add(item); written.Add(item); newItems.Add(item);
                }
                var audit = new DegisiklikGecmisi { Id = Guid.NewGuid(), VarlikTuru = nameof(DersSecimi), VarlikAnahtari = list.Id.ToString(),
                    Islem = correction ? "DersListesiDuzelt" : previous is null ? "DersListesiGonder" : "DersListesiYenidenGonder", YapanKullaniciId = actorId, IslemAni = now, OncekiDeger = previous,
                    YeniDeger = JsonSerializer.Serialize(new { Liste = list, Gerekce = reason, Kalemler = old.Concat(newItems) }) };
                db.DegisiklikGecmisi.Add(audit); written.Add(audit);
                written.Add(await DersListeSurumKaydi.EkleAsync(db, list, old.Concat(newItems), actorId, now, reason, ct));
                await db.SaveChangesAsync(ct);
                if (own is not null) await own.CommitAsync(ct);
                complete = true; return new(DersListeSonucu.Basarili, list.Id);
            }
            finally
            {
                if (!complete)
                {
                    if (own is null) await tx.RollbackToSavepointAsync(point, CancellationToken.None);
                    else await own.RollbackAsync(CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException) { return Result(DersListeSonucu.Cakisma); }
        catch (Exception ex) when (ex.GetBaseException() is SqlException sql && sql.Number is 1205 or 2601 or 2627) { return Result(DersListeSonucu.Cakisma); }
        catch (Exception ex)
        {
            logger.LogWarning("Ders listesi işlemi tamamlanamadı. Tür: {HataTuru}, SQL: {Kod}", ex.GetType().Name, (ex.GetBaseException() as SqlException)?.Number);
            return Result(DersListeSonucu.Basarisiz);
        }
        finally { if (!complete) foreach (var row in written) db.Entry(row).State = EntityState.Detached; }
    }
    private bool Acik(DersKayitKapisi? gate) => gate is not null && KayitKapisiKurali.EsikleriUygula(KayitKapisiServisi.Durum(gate), clock.GetUtcNow()).AcikMi;
    private void TakipTemizle<T>(Guid id) where T : class
    {
        var tracked = db.ChangeTracker.Entries<T>().SingleOrDefault(e => (Guid)e.Property("Id").CurrentValue! == id);
        if (tracked is not null) tracked.State = EntityState.Detached;
    }
}
