using System.IO;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Kapsula.Services;

/// <summary>Распознавание текста на снимке средствами Windows (офлайн, без интернета).</summary>
public static class OcrService
{
    public static bool Available
    {
        get
        {
            try
            {
                return OcrEngine.AvailableRecognizerLanguages.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static async Task<string> RecognizeAsync(BitmapSource image)
    {
        var path = Path.Combine(Path.GetTempPath(), "kapsula_ocr.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using (var file = File.Create(path)) encoder.Save(file);

        var storage = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await storage.OpenAsync(FileAccessMode.Read);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var engine = CreateEngine();
        if (engine is null) return "";

        var result = await engine.RecognizeAsync(bitmap);
        return result.Text;
    }

    // Русский пакет распознавания есть не на каждой Windows — берём первый доступный.
    private static OcrEngine? CreateEngine()
    {
        foreach (var tag in new[] { "ru-RU", "en-US" })
        {
            try
            {
                var engine = OcrEngine.TryCreateFromLanguage(new Language(tag));
                if (engine is not null) return engine;
            }
            catch
            {
            }
        }

        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch
        {
            return null;
        }
    }
}
