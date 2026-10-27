using SkiaSharp;

namespace CapYandal.Web.Services;

public static class ProfilFotografi
{
    public const int DosyaSiniri = 1024 * 1024;

    // Yüklenen dosya yerine denetlenmiş tek karelik fotoğraf saklanır.
    public static byte[] Hazirla(byte[] input, string fileName, string contentType)
    {
        if (input.Length is < 1 or > DosyaSiniri)
            throw new ArgumentException("Fotoğraf en fazla 1 MB olabilir.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png") || contentType is not ("image/jpeg" or "image/png"))
            throw new ArgumentException("JPG veya PNG fotoğraf seçin.");
        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png)
            || codec.FrameCount > 1)
            throw new ArgumentException("Geçerli, hareketsiz JPG veya PNG fotoğraf seçin.");
        var jpeg = codec.EncodedFormat == SKEncodedImageFormat.Jpeg;
        if ((jpeg && (extension == ".png" || contentType != "image/jpeg"))
            || (!jpeg && (extension != ".png" || contentType != "image/png")))
            throw new ArgumentException("Dosya türü ile fotoğraf içeriği eşleşmiyor.");
        if (!jpeg && AnimatedPng(input))
            throw new ArgumentException("Hareketli PNG yerine tek kare fotoğraf seçin.");
        var info = codec.Info;
        if (info.Width is < 1 or > 4096 || info.Height is < 1 or > 4096 || (long)info.Width * info.Height > 12_000_000)
            throw new ArgumentException("Fotoğraf çözünürlüğü çok büyük veya geçersiz.");
        var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = rotated ? info.Height : info.Width;
        var height = rotated ? info.Width : info.Height;
        if (width < 300 || height < 400)
            throw new ArgumentException("Fotoğraf en az 300×400 piksel olmalıdır.");
        using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("Fotoğraf okunamadı. Başka bir dosya seçin.");
        using var upright = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(upright))
        using (var sourceImage = SKImage.FromBitmap(decoded))
        {
            canvas.Clear(SKColors.White);
            canvas.SetMatrix(Orientation(codec.EncodedOrigin, info.Width, info.Height));
            canvas.DrawImage(sourceImage, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }
        using var normalized = new SKBitmap(300, 400);
        using (var canvas = new SKCanvas(normalized))
        using (var image = SKImage.FromBitmap(upright))
        {
            canvas.Clear(SKColors.White);
            // Fotoğrafı bozmadan ortadan kırparak çerçeveye yerleştirir.
            var scale = Math.Max(300f / width, 400f / height);
            var x = (300 - width * scale) / 2;
            var y = (400 - height * scale) / 2;
            canvas.DrawImage(image, new SKRect(x, y, x + width * scale, y + height * scale),
                new SKSamplingOptions(SKFilterMode.Linear));
        }
        using var final = SKImage.FromBitmap(normalized);
        using var encoded = final.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    private static bool AnimatedPng(byte[] input)
    {
        var offset = 8;
        while (offset + 12 <= input.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset, 4));
            if (length > input.Length - offset - 12) break;
            if (length == 8 && input.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8)
                && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset + 8, 4)) > 1)
                return true;
            offset += (int)length + 12;
        }
        return false;
    }

    private static SKMatrix Orientation(SKEncodedOrigin origin, int w, int h)
    {
        var (a, b, c, d, e, f) = origin switch
        {
            SKEncodedOrigin.TopRight => (-1, 0, w, 0, 1, 0),
            SKEncodedOrigin.BottomRight => (-1, 0, w, 0, -1, h),
            SKEncodedOrigin.BottomLeft => (1, 0, 0, 0, -1, h),
            SKEncodedOrigin.LeftTop => (0, 1, 0, 1, 0, 0),
            SKEncodedOrigin.RightTop => (0, -1, h, 1, 0, 0),
            SKEncodedOrigin.RightBottom => (0, -1, h, -1, 0, w),
            SKEncodedOrigin.LeftBottom => (0, 1, 0, -1, 0, w),
            _ => (1, 0, 0, 0, 1, 0)
        };
        return new SKMatrix { ScaleX = a, SkewX = b, TransX = c, SkewY = d, ScaleY = e, TransY = f, Persp2 = 1 };
    }
}
