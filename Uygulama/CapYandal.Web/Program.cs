using CapYandal.Web.Data;
using CapYandal.Web.Data.Entities;
using CapYandal.Web.Security;
using CapYandal.Web.Services.Eposta;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Canlı/staging yayınında belge ve şifreleme anahtarları yeniden dağıtımda kaybolmayan App_Data'da tutulur.
var configuredDataRoot = builder.Configuration["Depolama:KokDizin"];
var dataRoot = string.IsNullOrWhiteSpace(configuredDataRoot)
    ? Path.Combine(builder.Environment.ContentRootPath, "App_Data")
    : Path.GetFullPath(configuredDataRoot, builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataRoot);
var keyRoot = Path.Combine(dataRoot, "DataProtectionKeys");
Directory.CreateDirectory(keyRoot);
builder.Services.AddDataProtection()
    .SetApplicationName("CapYandal.OgrenciTakip")
    .PersistKeysToFileSystem(new DirectoryInfo(keyRoot));

var connectionString = builder.Configuration.GetConnectionString("CapYandal")
    ?? throw new InvalidOperationException("Proje veritabanı bağlantısı yapılandırılmalıdır.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHesapGuvenligi();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.OgrenciKayitServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.ProgramKararServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.ProgramKararUyariServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.ProgramTakipNotuServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.DonemIzniServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Ogrenci.GanoKayitServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Gano.GanoEkranServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Hesap.PersonelGorevAtamaServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Paneller.PanelModelServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Paneller.PersonelHesapPanelServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Hesap.PersonelErisimServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Hesap.GorusmeBilgisiServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Gano.GanoSonucArsivServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Gano.AkademikKosulServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Gano.SayisalKosulServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Dersler.SablonKayitServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Dersler.MufredatYonetimiServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Dersler.KaynakAnaDalServisi>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.KayitKapisiServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.DersOnayServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.DersListeServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.DersDenemeServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.GecmisDersAktarimServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.DersKaydi.DersListeOkumaServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Sohbet.SohbetServisi>();
// Kayıt açıklığı mevcut tarih kuralıyla okuma ve gönderim anında hesaplanır; periyodik SQL yok.
builder.Services.AddSingleton(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new CapYandal.Web.Services.Belgeler.BelgeDeposu(
        Path.Combine(dataRoot, "GenelBelgeler"),
        env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
        new CapYandal.Web.Services.Belgeler.BelgeIcerikDenetimi(),
        sp.GetRequiredService<ILogger<CapYandal.Web.Services.Belgeler.BelgeDeposu>>());
});
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.GenelBelgeKayitServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.GenelBelgeArsivServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.GenelBelgeOkumaServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.KisiselBelgeOkumaServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.EvrakKayitServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.BelgeIncelemeServisi>();
builder.Services.AddScoped<CapYandal.Web.Services.Belgeler.EvrakOkumaServisi>();
// EvrakSilmeServisi kaldırıldı; arşiv belgeleri silinemez.
builder.Services.AddScoped<CapYandal.Web.Controllers.IGenelBelgeOkumaKaynagi>(sp => sp.GetRequiredService<CapYandal.Web.Services.Belgeler.GenelBelgeOkumaServisi>());
builder.Services.AddScoped<CapYandal.Web.Controllers.IGenelBelgeGuncelErisim>(sp => sp.GetRequiredService<CapYandal.Web.Services.Belgeler.GenelBelgeOkumaServisi>());
builder.Services.AddEpostaGonderimi(builder.Configuration);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<HesapKurulumuFiltresi>();
}).AddCookieTempDataProvider(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);

var app = builder.Build();

// Hata/kimlik/HTML ve dosya yanıtlarında da aynı tarayıcı savunmaları uygulanır.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        // Mevcut dinamik boyut/stil kullanımı için yalnız stillerde inline izni gerekir.
        // Betiklerde inline/eval/haricî kaynak izni yoktur.
        if (!headers.ContainsKey("Content-Security-Policy"))
            headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; frame-src 'self' blob:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
        return Task.CompletedTask;
    });
    await next(context);
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
// Yetki denetiminden sonra, CSRF/model binding gövdeyi okumadan önce bilinen büyük gövdeyi reddet.
// Chunked isteklerde mevcut Kestrel/RequestSizeLimit ve gerçek dosya boyutu denetimi sürer.
app.Use(async (context,next) =>
{
    var limit=context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
    if(limit is { } max && context.Request.ContentLength is { } size && size>max) {context.Response.StatusCode=413;return;}
    await next(context);
});
app.UseRateLimiter();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program { }

