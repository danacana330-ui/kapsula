using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kapsula;

/// <summary>Цвета и типографика приложения. Один источник правды для всего UI.</summary>
public static class Theme
{
    // матовое стекло: фон на 85 %, чтобы сквозь него просвечивал рабочий стол
    public static readonly Color DarkBg = Color.FromArgb(0xD9, 0x16, 0x18, 0x1D);
    public static readonly Color DarkCard = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkCardHover = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkStroke = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkStrokeRim = Color.FromArgb(0x5E, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkText = Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkTextDim = Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

    public static readonly Color LightBg = Color.FromArgb(0xBF, 0xFF, 0xFF, 0xFF);
    public static readonly Color LightCard = Color.FromArgb(0x14, 0x1B, 0x1B, 0x1B);
    public static readonly Color LightCardHover = Color.FromArgb(0x22, 0x1B, 0x1B, 0x1B);
    public static readonly Color LightStroke = Color.FromArgb(0x1E, 0x00, 0x00, 0x00);
    public static readonly Color LightStrokeRim = Color.FromArgb(0x5A, 0x00, 0x00, 0x00);
    public static readonly Color LightText = Color.FromArgb(0xF7, 0x1A, 0x1A, 0x1A);
    public static readonly Color LightTextDim = Color.FromArgb(0xB3, 0x1A, 0x1A, 0x1A);

    /// <summary>Полный янтарь — только там, где идёт жизнь; два тише — тот же цвет, прилитый к фону.</summary>
    public static readonly Color Accent = Color.FromRgb(0xFF, 0xB8, 0x6B);
    public static readonly Color AccentSoft = Color.FromRgb(0xB9, 0x88, 0x54);
    public static readonly Color AccentQuiet = Color.FromRgb(0x8A, 0x64, 0x40);
    public static readonly Color Accent2 = Color.FromRgb(0xFF, 0x7A, 0x6B);

    private static readonly Color DarkHover = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
    private static readonly Color LightHover = Color.FromArgb(0x1C, 0x00, 0x00, 0x00);

    // Кисти живут долго: XAML берёт их через DynamicResource, а смена цвета красит весь UI на ходу.
    // не замораживаем: цвет меняется на ходу при переключении темы
    public static readonly SolidColorBrush BgBrush = new(DarkBg);
    public static readonly SolidColorBrush CardBrush = new(DarkCard);
    public static readonly SolidColorBrush CardHoverBrush = new(DarkCardHover);
    public static readonly SolidColorBrush StrokeBrush = new(DarkStroke);
    public static readonly SolidColorBrush TextBrush = new(DarkText);
    public static readonly SolidColorBrush DimBrush = new(DarkTextDim);
    public static readonly SolidColorBrush HoverBrush = new(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    public static readonly SolidColorBrush AccentBrush = Frozen(Accent);
    public static readonly SolidColorBrush AccentSoftBrush = Frozen(AccentSoft);
    public static readonly SolidColorBrush AccentQuietBrush = Frozen(AccentQuiet);
    public static readonly LinearGradientBrush AccentGradient = FrozenGradient();

    /// <summary>Зерно стекла: тайл шума поверх фона капсулы.</summary>
    public static readonly ImageBrush NoiseBrush = BuildNoise();

    /// <summary>Кромки стекла: сверху ловит свет, снизу отделяет от стола.</summary>
    public static readonly SolidColorBrush EdgeTopBrush = new(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
    public static readonly SolidColorBrush EdgeBottomBrush = new(Color.FromArgb(0x59, 0x00, 0x00, 0x00));

    /// <summary>
    /// Отдать кисти словарю приложения: стили ссылаются на них по имени, поэтому
    /// смена темы доходит до всех элементов без перезапуска. Копии, потому что
    /// словарь замораживает то, что в нём лежит.
    /// </summary>
    public static void RegisterResources(ResourceDictionary resources)
    {
        resources["Bg"] = new SolidColorBrush(DarkBg);
        resources["Card"] = new SolidColorBrush(DarkCard);
        resources["CardHover"] = new SolidColorBrush(DarkCardHover);
        resources["Stroke"] = new SolidColorBrush(DarkStroke);
        resources["Text"] = new SolidColorBrush(DarkText);
        resources["Dim"] = new SolidColorBrush(DarkTextDim);
        resources["Hover"] = new SolidColorBrush(DarkHover);
    }

    /// <summary>Подменить кисти в словаре: элементы со ссылкой по имени перекрасятся сами.</summary>
    private static void RefreshResources()
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        resources["Bg"] = new SolidColorBrush(BgBrush.Color);
        resources["Card"] = new SolidColorBrush(CardBrush.Color);
        resources["CardHover"] = new SolidColorBrush(CardHoverBrush.Color);
        resources["Stroke"] = new SolidColorBrush(StrokeBrush.Color);
        resources["Text"] = new SolidColorBrush(TextBrush.Color);
        resources["Dim"] = new SolidColorBrush(DimBrush.Color);
        resources["Hover"] = new SolidColorBrush(HoverBrush.Color);
    }

    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI");

    public const double CollapsedWidth = 400;
    public const double CollapsedHeight = 46;
    public const double IdleWidth = 300;
    public const double IdleHeight = 38;
    public const double ExpandedWidth = 760;
    public const double ExpandedHeight = 350;
    public const double Radius = 23;

    public static bool IsLight { get; private set; }
    public static bool IsRim { get; private set; }

    public static void SetLight(bool light)
    {
        IsLight = light;
        Paint(BgBrush, light ? LightBg : DarkBg);
        Paint(CardBrush, light ? LightCard : DarkCard);
        Paint(CardHoverBrush, light ? LightCardHover : DarkCardHover);
        Paint(TextBrush, light ? LightText : DarkText);
        Paint(DimBrush, light ? LightTextDim : DarkTextDim);
        Paint(HoverBrush, light ? LightHover : DarkHover);
        Paint(EdgeTopBrush, light ? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        Paint(EdgeBottomBrush, light ? Color.FromArgb(0x26, 0x00, 0x00, 0x00) : Color.FromArgb(0x59, 0x00, 0x00, 0x00));
        ApplyStroke();
        RefreshResources();
    }

    public static void SetRim(bool rim)
    {
        IsRim = rim;
        ApplyStroke();
        RefreshResources();
    }

    private static void ApplyStroke()
    {
        Paint(StrokeBrush, IsLight
            ? (IsRim ? LightStrokeRim : LightStroke)
            : (IsRim ? DarkStrokeRim : DarkStroke));
    }

    /// <summary>Запечатанную стилем кисть перекрасить нельзя — такую просто пропускаем.</summary>
    private static void Paint(SolidColorBrush brush, Color color)
    {
        if (brush.IsFrozen) return;
        brush.Color = color;
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static LinearGradientBrush FrozenGradient()
    {
        var g = new LinearGradientBrush(Accent, Accent2, new Point(0, 0), new Point(1, 1));
        g.Freeze();
        return g;
    }

    /// <summary>Тайл серого шума 64×64: на нём и держится ощущение матового стекла.</summary>
    private static ImageBrush BuildNoise()
    {
        const int side = 64;
        var pixels = new byte[side * side * 4];
        var rnd = new Random(17);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var v = (byte)rnd.Next(0, 256);
            pixels[i] = v;
            pixels[i + 1] = v;
            pixels[i + 2] = v;
            pixels[i + 3] = 0xFF;
        }

        var bitmap = new WriteableBitmap(side, side, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, side, side), pixels, side * 4, 0);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, side, side),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    public static BitmapImage? LoadImage(byte[]? data)
    {
        if (data is null || data.Length == 0) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(data);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }
}
