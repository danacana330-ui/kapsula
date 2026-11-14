using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kapsula;

/// <summary>Цвета и типографика приложения. Один источник правды для всего UI.</summary>
public static class Theme
{
    public static readonly Color DarkBg = Color.FromArgb(0xF4, 0x16, 0x18, 0x1D);
    public static readonly Color DarkCard = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkCardHover = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkStroke = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkStrokeRim = Color.FromArgb(0x5E, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkText = Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkTextDim = Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF);

    public static readonly Color LightBg = Color.FromArgb(0xF5, 0xF7, 0xF4, 0xEC);
    public static readonly Color LightCard = Color.FromArgb(0x14, 0x1B, 0x1B, 0x1B);
    public static readonly Color LightCardHover = Color.FromArgb(0x22, 0x1B, 0x1B, 0x1B);
    public static readonly Color LightStroke = Color.FromArgb(0x1E, 0x00, 0x00, 0x00);
    public static readonly Color LightStrokeRim = Color.FromArgb(0x5A, 0x00, 0x00, 0x00);
    public static readonly Color LightText = Color.FromArgb(0xF7, 0x1A, 0x1A, 0x1A);
    public static readonly Color LightTextDim = Color.FromArgb(0x8C, 0x1A, 0x1A, 0x1A);

    public static readonly Color Accent = Color.FromRgb(0xFF, 0xB8, 0x6B);
    public static readonly Color Accent2 = Color.FromRgb(0xFF, 0x7A, 0x6B);

    // Кисти живут долго: XAML берёт их через x:Static, а смена цвета красит весь UI на ходу.
    // не замораживаем: цвет меняется на ходу при переключении темы
    public static readonly SolidColorBrush BgBrush = new(DarkBg);
    public static readonly SolidColorBrush CardBrush = new(DarkCard);
    public static readonly SolidColorBrush CardHoverBrush = new(DarkCardHover);
    public static readonly SolidColorBrush StrokeBrush = new(DarkStroke);
    public static readonly SolidColorBrush TextBrush = new(DarkText);
    public static readonly SolidColorBrush DimBrush = new(DarkTextDim);
    public static readonly SolidColorBrush HoverBrush = new(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    public static readonly SolidColorBrush AccentBrush = Frozen(Accent);
    public static readonly LinearGradientBrush AccentGradient = FrozenGradient();

    public static readonly FontFamily Font = new("Segoe UI Variable Display, Segoe UI");

    public const double CollapsedWidth = 400;
    public const double CollapsedHeight = 46;
    public const double IdleWidth = 300;
    public const double IdleHeight = 38;
    public const double ExpandedWidth = 760;
    public const double ExpandedHeight = 560;
    public const double Radius = 23;

    public static bool IsLight { get; private set; }
    public static bool IsRim { get; private set; }

    public static void SetLight(bool light)
    {
        IsLight = light;
        BgBrush.Color = light ? LightBg : DarkBg;
        CardBrush.Color = light ? LightCard : DarkCard;
        CardHoverBrush.Color = light ? LightCardHover : DarkCardHover;
        TextBrush.Color = light ? LightText : DarkText;
        DimBrush.Color = light ? LightTextDim : DarkTextDim;
        HoverBrush.Color = light ? Color.FromArgb(0x1C, 0x00, 0x00, 0x00) : Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
        ApplyStroke();
    }

    public static void SetRim(bool rim)
    {
        IsRim = rim;
        ApplyStroke();
    }

    private static void ApplyStroke()
    {
        StrokeBrush.Color = IsLight
            ? (IsRim ? LightStrokeRim : LightStroke)
            : (IsRim ? DarkStrokeRim : DarkStroke);
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
