using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Kapsula;

/// <summary>Насколько плитка главная: крупная, иконка в строке или тихая внизу.</summary>
public enum TileKind
{
    Large,
    Small,
    Quiet,
}

public sealed record TileDef(string Id, string Title, string Glyph, TileKind Kind);

/// <summary>Плитки главного экрана: состав и порядок берутся из настроек.</summary>
public static class Tiles
{
    public static readonly TileDef[] All =
    {
        new("clipboard", "Буфер", "\uE77F", TileKind.Large),
        new("notes", "Заметки", "\uE70B", TileKind.Large),
        new("ai", "ИИ", "\uE8BD", TileKind.Large),
        new("capture", "Захват экрана", "\uE722", TileKind.Large),
        new("shelf", "Полка", "\uE8B7", TileKind.Small),
        new("timer", "Таймер", "\uE823", TileKind.Small),
        new("weather", "Погода", "\uE9CA", TileKind.Small),
        new("quick", "Быстро", "\uE945", TileKind.Small),
        new("summary", "Сводка", "\uE787", TileKind.Quiet),
        new("appearance", "Настройки", "\uE713", TileKind.Quiet),
    };

    public static Button Create(TileDef def, Style style, RoutedEventHandler onClick, out TextBlock? label)
    {
        var glyph = new TextBlock
        {
            Text = def.Glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = def.Kind switch { TileKind.Large => 20, TileKind.Small => 15, _ => 12 },
            Foreground = Theme.AccentSoftBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        label = null;
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        // погоду рисуем вектором: у шрифтовых значков облако читается плохо
        UIElement mark = glyph;
        if (def.Id == "weather")
        {
            mark = new WeatherIcon { Kind = "cloud", Width = 18, Height = 18 };
        }

        if (def.Kind == TileKind.Quiet)
        {
            content.Orientation = Orientation.Horizontal;
            content.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(0, 0, 6, 0);
            content.Children.Add(glyph);
        }
        else
        {
            content.Children.Add(mark);
        }

        if (def.Kind == TileKind.Large)
        {
            label = new TextBlock
            {
                Text = def.Title,
                FontSize = 10.5,
                Margin = new Thickness(0, 5, 0, 0),
                Foreground = Theme.DimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
            content.Children.Add(label);
        }
        else if (def.Kind == TileKind.Quiet)
        {
            label = new TextBlock
            {
                Text = def.Title,
                FontSize = 10.5,
                Foreground = Theme.DimBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Children.Add(label);
        }

        var button = new Button
        {
            Style = style,
            Content = content,
            Tag = def.Id,
            Margin = new Thickness(2),
            ToolTip = def.Kind == TileKind.Small ? def.Title : null,
        };
        button.Click += onClick;
        if (mark is TextBlock textGlyph) TilesGlow.Apply(button, textGlyph);
        else TilesGlow.ApplyVector(button, mark);
        return button;
    }
}

/// <summary>Общий приём подсветки: полный янтарь и свечение, пока курсор на плитке.</summary>
internal static class TilesGlow
{
    public static void Apply(FrameworkElement host, TextBlock glyph)
    {
        var effect = new DropShadowEffect
        {
            Color = Theme.Accent,
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.45,
        };

        host.MouseEnter += (_, _) =>
        {
            glyph.Foreground = Theme.AccentBrush;
            glyph.Effect = effect;
        };
        host.MouseLeave += (_, _) =>
        {
            glyph.Foreground = Theme.AccentSoftBrush;
            glyph.Effect = null;
        };
    }

    /// <summary>То же для рисованного значка: цвет внутри фигуры не перекрасить, подсвечиваем свечением.</summary>
    public static void ApplyVector(FrameworkElement host, UIElement visual)
    {
        var effect = new DropShadowEffect
        {
            Color = Theme.Accent,
            BlurRadius = 14,
            ShadowDepth = 0,
            Opacity = 0.75,
        };

        host.MouseEnter += (_, _) =>
        {
            visual.Opacity = 1;
            visual.Effect = effect;
        };
        host.MouseLeave += (_, _) =>
        {
            visual.Opacity = 0.85;
            visual.Effect = null;
        };
    }
}

/// <summary>
/// «Захват экрана»: в покое одна плитка, при наведении расходится на снимок и запись.
/// Нарочно без увеличения — иначе кнопка уезжает из-под курсора в момент нажатия.
/// </summary>
public sealed class CaptureTile : Grid
{
    private readonly Border _gap;
    private readonly Border _overlay;
    private readonly Button _shot;
    private readonly Button _record;

    /// <summary>true — запись экрана, false — снимок.</summary>
    public event Action<bool>? Picked;

    public CaptureTile(Style halfStyle)
    {
        // без собственного фона сетка не участвует в попадании мыши,
        // а половины в покое прозрачны — наведение тогда не ловится вовсе
        Background = Brushes.Transparent;

        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition());

        _shot = Half(halfStyle, "\uE722", "Снимок", false);
        _record = Half(halfStyle, "\uE7C8", "Запись", true);
        SetColumn(_shot, 0);
        SetColumn(_record, 2);
        Children.Add(_shot);
        Children.Add(_record);

        _gap = new Border { Width = 0 };
        SetColumn(_gap, 1);
        Children.Add(_gap);

        var icon = new TextBlock
        {
            Text = "\uE722",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = Theme.AccentSoftBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var caption = new TextBlock
        {
            Text = "Захват экрана",
            FontSize = 10.5,
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = Theme.DimBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _overlay = new Border
        {
            Background = Theme.CardBrush,
            BorderBrush = Theme.StrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(2),
            IsHitTestVisible = false,
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { icon, caption },
            },
        };
        SetColumn(_overlay, 0);
        SetColumnSpan(_overlay, 3);
        Children.Add(_overlay);

        // в покое половины не видны совсем: иначе сквозь обложку читаются их подписи
        _shot.Opacity = 0;
        _record.Opacity = 0;
        _shot.IsHitTestVisible = false;
        _record.IsHitTestVisible = false;

        MouseEnter += (_, _) => Split(true);
        MouseLeave += (_, _) => Split(false);
    }

    private Button Half(Style style, string glyph, string caption, bool record)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = Theme.AccentSoftBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var text = new TextBlock
        {
            Text = caption,
            FontSize = 10,
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = Theme.DimBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        var button = new Button
        {
            Style = style,
            Margin = new Thickness(2),
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { icon, text },
            },
        };
        button.Click += (_, _) => Picked?.Invoke(record);
        TilesGlow.Apply(button, icon);
        return button;
    }

    private void Split(bool open)
    {
        _gap.BeginAnimation(WidthProperty, new DoubleAnimation(open ? 4 : 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        var fade = new DoubleAnimation(open ? 1 : 0, TimeSpan.FromMilliseconds(120));
        _shot.BeginAnimation(OpacityProperty, fade);
        _record.BeginAnimation(OpacityProperty, fade);
        _overlay.BeginAnimation(OpacityProperty,
            new DoubleAnimation(open ? 0 : 1, TimeSpan.FromMilliseconds(120)));

        // пока половины спрятаны, они не должны ловить нажатия вместо общей плитки
        _shot.IsHitTestVisible = open;
        _record.IsHitTestVisible = open;
    }
}
