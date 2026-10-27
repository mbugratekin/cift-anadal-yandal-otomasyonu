using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace CapYandal.Web.Services.Belgeler;

public sealed record DepolananBelge(string Anahtar,string Mime,long Boyut,string Sha256);

// Yetki ve DB sürüm işlemi çağıran servistedir. Dizin yalnız uygulama hesabınca yazılabilir olmalıdır.
public sealed class BelgeDeposu
{
    private readonly string kok;
    private static readonly Regex AnahtarBicimi=new(@"\A[0-9a-f]{32}\.(pdf|docx)\z",RegexOptions.CultureInvariant);
    private readonly BelgeIcerikDenetimi denetim;
    private readonly ILogger<BelgeDeposu> logger;
    public BelgeDeposu(string ozelDizin,string webRoot, BelgeIcerikDenetimi? icerikDenetimi = null, ILogger<BelgeDeposu>? gunluk = null)
    {
        denetim = icerikDenetimi ?? new();
        logger = gunluk ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BelgeDeposu>.Instance;
        if(!Path.IsPathFullyQualified(ozelDizin)||!Path.IsPathFullyQualified(webRoot))throw new ArgumentException("Mutlak dizin gerekir.");
        kok=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ozelDizin));
        var web=Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRoot));
        var compare=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(kok.Equals(web,compare)||kok.StartsWith(web+Path.DirectorySeparatorChar,compare)||web.StartsWith(kok+Path.DirectorySeparatorChar,compare))
            throw new ArgumentException("Depo web kökünden ayrı olmalıdır.");
        DizinDogrula();Directory.CreateDirectory(kok);DizinDogrula();
    }
    private void DizinDogrula()
    {
        for(var dir=new DirectoryInfo(kok);dir is not null;dir=dir.Parent)
            if(dir.LinkTarget is not null || (dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0))throw new IOException("Bağlantılı depo dizini kabul edilmez.");
    }
    private string Yol(string key)
    {
        if(!AnahtarBicimi.IsMatch(key))throw new ArgumentException("Depo anahtarı geçersiz.");
        DizinDogrula();return Path.Combine(kok,key);
    }
    public async Task<DepolananBelge> KaydetAsync(IFormFile? dosya,CancellationToken iptal=default)
    {
        iptal.ThrowIfCancellationRequested();
        var result=await BelgeDosyaOkuyucu.OkuAsync(dosya,iptal);
        if(!result.Basarili)throw new InvalidDataException(result.Hata);
        string suffix=result.GuvenliMime=="application/pdf"?".pdf":".docx";
        var key=Guid.NewGuid().ToString("N")+suffix;
        var metadata=new DepolananBelge(key,result.GuvenliMime!,result.Icerik!.LongLength,Convert.ToHexString(SHA256.HashData(result.Icerik)));
        var target=Yol(key);var staging=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        bool created=false;
        try
        {
            await using(var file=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.Asynchronous))
            {
                created=true;await file.WriteAsync(result.Icerik!,iptal);await file.FlushAsync(iptal);file.Flush(flushToDisk:true);
            }
            await denetim.DogrulaAsync(staging, iptal);
            iptal.ThrowIfCancellationRequested();DizinDogrula();
            File.Move(staging,target,overwrite:false);created=false;
            // Move başarılı olduktan sonra iptal hatası üretme: çağıran dosyanın yazıldığını bilmelidir.
            return metadata;
        }
        finally
        {
            if (created)
            {
                try { File.Delete(staging); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    logger.LogCritical("Yayımlanmamış belge geçici dosyası temizlenemedi. İşlem: {Kimlik}, Hata: {Tur}", key, error.GetType().Name);
                    throw new IOException("Geçici belge temizliği tamamlanamadı; yönetici kontrolü gerekir.");
                }
            }
        }
    }
    public async Task<FileStream> AcAsync(DepolananBelge metadata,CancellationToken iptal=default)
    {
        iptal.ThrowIfCancellationRequested();
        var expectedMime=metadata.Anahtar.EndsWith(".pdf",StringComparison.Ordinal)?"application/pdf":"application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if(metadata.Mime!=expectedMime)throw new InvalidDataException("Dosya içerik türü eşleşmiyor.");
        var path=Yol(metadata.Anahtar);
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Bağlantılı dosya kabul edilmez.");
        var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,8192,FileOptions.Asynchronous);
        try
        {
            if(metadata.Boyut is <=0 or >BelgeSurumKurali.DosyaSiniri ||file.Length!=metadata.Boyut)
                throw new InvalidDataException("Dosya boyutu eşleşmiyor.");
            var expected=Convert.FromHexString(metadata.Sha256);
            var actual=await SHA256.HashDataAsync(file,iptal);
            if(expected.Length!=32||!CryptographicOperations.FixedTimeEquals(expected,actual))throw new InvalidDataException("Dosya özeti eşleşmiyor.");
            file.Position=0;return file;
        }
        catch{await file.DisposeAsync();throw;}
    }
    // Yalnız yetkili servisçe DB'den okunmuş exact anahtar veya bu çağrının ürettiği anahtar.
    // Genel belge geçmişine otomatik temizlik uygulanmaz; erişim iptali önce DB'de tamamlanır.
    public void Kaldir(string anahtar)
    {
        var path=Yol(anahtar);
        try {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("Bağlantılı dosya kabul edilmez.");
            File.Delete(path);
        }
        catch(FileNotFoundException) { } // Kesinti sonrası aynı kimlik için güvenli tekrar.
    }
}


