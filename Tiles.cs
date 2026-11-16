using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kapsula;

public sealed record TileDef(string Id, string Title, string Glyph);

/// <summary>Плитки главного экрана: состав и порядок берутся из настроек.</summary>
public static class Tiles
{
    public static readonly TileDef[] All =
    {
        new("clipboard", "Буфер", "\uE77F"),
        new("shelf", "Полка", "\uE8B7"),
        new("notes", "Заметки", "\uE70B"),
        new("ai", "ИИ", "\uE8BD"),
        new("shot", "Скриншот", "\uE722"),
        new("weather", "Погода", "\uE706"),
        new("quick", "Быстро", "\uE713"),
        new("timer", "Таймер", "\uE823"),
        new("record", "Запись", "\uE7C8"),
        new("summary", "Сводка", "\uE787"),
        new("appearance", "Вид", "\uE790"),
        new("system", "Система", "\uE950"),
    };

    public static Button Create(TileDef def, Style tileStyle, RoutedEventHandler onClick, out TextBlock label)
    {
        var glyph = new TextBlock
        {
            Text = def.Glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            Foreground = Theme.AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        label = new TextBlock
        {
            Text = def.Title,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = Theme.TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(glyph);
        content.Children.Add(label);

        var button = new Button
        {
            Style = tileStyle,
            Content = content,
            Tag = def.Id,
            Margin = new Thickness(3),
        };
        button.Click += onClick;
        return button;
    }
}
