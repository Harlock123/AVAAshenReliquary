using SkiaSharp;

namespace AshenReliquary.App.Rendering;

/// <summary>
/// Sprite frames and tiles pre-scaled to the current zoom. Each is cut out of its sheet and resampled once with a
/// high-quality filter, so drawing it each frame is a plain pixel copy: no per-pixel filtering, which is what
/// makes software (CPU) rendering fast enough. Cutting frames out one by one also stops neighbouring frames in
/// Flare's tightly packed sheets from bleeding into each other.
/// </summary>
internal sealed class SpriteCache(FlareAssets assets, SKColorFilter? grade = null)
{
    private static readonly SKSamplingOptions Resample = new(SKCubicResampler.Mitchell);
    private readonly Dictionary<(string Sheet, int X, int Y, int W, int H), SKImage> _cache = [];
    private float _scale = -1;

    public int Count => _cache.Count;

    /// <summary>The frame at <paramref name="scale"/>; switching scale (a zoom step) drops the old frames.</summary>
    public SKImage Get(string sheet, int x, int y, int width, int height, float scale)
    {
        if (Math.Abs(scale - _scale) > 1e-4)
        {
            foreach (var image in _cache.Values)
                image.Dispose();
            _cache.Clear();
            _scale = scale;
        }
        var key = (sheet, x, y, width, height);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var source = assets.Image(sheet);
        var w = Math.Max(1, (int)Math.Round(width * scale));
        var h = Math.Max(1, (int)Math.Round(height * scale));
        using var cut = source.Subset(SKRectI.Create(x, y, width, height)) ?? source;
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var pixmap = bitmap.PeekPixels())
            cut.ScalePixels(pixmap, Resample);
        if (grade is not null)
        {
            // Bake the act's colour grade in, so the frame never pays for it again.
            using var graded = new SKBitmap(info);
            using (var canvas = new SKCanvas(graded))
            using (var paint = new SKPaint { ColorFilter = grade })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }
            graded.SetImmutable();
            var gradedImage = SKImage.FromBitmap(graded).ToRasterImage(ensurePixelData: true);
            _cache[key] = gradedImage;
            return gradedImage;
        }
        bitmap.SetImmutable();
        var scaled = SKImage.FromBitmap(bitmap).ToRasterImage(ensurePixelData: true);
        _cache[key] = scaled;
        return scaled;
    }
}
