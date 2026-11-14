using System.Windows;
using System.Windows.Controls;

namespace Kapsula;

/// <summary>Погодная иконка, нарисованная вектором: не зависит от шрифтов Windows.</summary>
public partial class WeatherIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(WeatherIcon),
        new PropertyMetadata("cloud", (d, _) => ((WeatherIcon)d).Apply()));

    public WeatherIcon()
    {
        InitializeComponent();
        Apply();
    }

    /// <summary>sun, partly, cloud, fog, rain, sleet, snow, thunder, clock.</summary>
    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private void Apply()
    {
        if (Root is null) return;

        var kind = string.IsNullOrWhiteSpace(Kind) ? "cloud" : Kind;

        Show(SunGroup, kind == "sun");
        Show(FogGroup, kind == "fog");
        Show(PartlyGroup, kind == "partly");
        Show(ClockGroup, kind == "clock");

        var cloudy = kind is "cloud" or "rain" or "sleet" or "snow" or "thunder";
        Show(CloudGroup, cloudy);
        Show(RainGroup, kind is "rain" or "sleet");
        Show(SnowGroup, kind is "snow" or "sleet");
        Show(ThunderGroup, kind == "thunder");
    }

    private static void Show(UIElement element, bool visible) =>
        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
}
