using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;

namespace Kapsula.Services;

/// <summary>Что можно сделать со скопированной строкой: ссылка, телефон, трек и так далее.</summary>
public sealed class SmartAction
{
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
}

/// <summary>
/// Разбор скопированного текста. Тип ищется внутри строки, а не «строка целиком»:
/// вокруг может быть «Смотри тут», кавычки, скобки, лишние запятые.
/// </summary>
public static class SmartText
{
    private static readonly Regex Link = new(@"https?://[^\s<>""'`]+", RegexOptions.IgnoreCase);
    private static readonly Regex Mail = new(@"[\w.+-]+@[\w-]+(\.[\w-]+)+");
    private static readonly Regex Phone = new(@"(?<![\d])(?:\+7|8)[\s\-(]*\d{3}[\s\-)]*\d{3}[\s\-]*\d{2}[\s\-]*\d{2}(?![\d])");
    private static readonly Regex Track = new(@"\b[A-Z]{2}\d{9}[A-Z]{2}\b");
    private static readonly Regex Coords = new(@"(-?\d{1,3}\.\d{3,})\s*[,;]\s*(-?\d{1,3}\.\d{3,})");
    private static readonly Regex Color = new(@"#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b");

    public static List<SmartAction> Detect(string text)
    {
        var found = new List<SmartAction>();
        if (string.IsNullOrWhiteSpace(text)) return found;

        var link = Link.Match(text);
        if (link.Success)
        {
            var url = Trim(link.Value);
            found.Add(new SmartAction { Kind = "link", Title = "Открыть " + Short(url), Value = url });
        }

        var track = Track.Match(text);
        if (track.Success && !link.Success)
            found.Add(new SmartAction { Kind = "track", Title = "Трек " + track.Value, Value = track.Value });

        var mail = Mail.Match(text);
        if (mail.Success)
            found.Add(new SmartAction { Kind = "mail", Title = "Написать " + mail.Value, Value = mail.Value });

        var phone = Phone.Match(text);
        if (phone.Success)
        {
            var digits = new string(phone.Value.Where(char.IsDigit).ToArray());
            if (digits.Length == 11) digits = "7" + digits[1..];
            found.Add(new SmartAction { Kind = "phone", Title = "Номер +" + digits, Value = digits });
        }

        var coords = Coords.Match(text);
        if (coords.Success)
            found.Add(new SmartAction { Kind = "coords", Title = "На карте", Value = $"{coords.Groups[1].Value},{coords.Groups[2].Value}" });

        var color = Color.Match(text);
        if (color.Success && !link.Success)
            found.Add(new SmartAction { Kind = "color", Title = "Цвет " + color.Value.ToUpperInvariant(), Value = color.Value.ToUpperInvariant() });

        return found;
    }

    public static void Perform(SmartAction action)
    {
        var url = action.Kind switch
        {
            "link" => action.Value,
            "mail" => "mailto:" + action.Value,
            "track" => "https://t.17track.net/ru#nums=" + action.Value,
            "coords" => CoordsUrl(action.Value),
            _ => null,
        };

        if (url is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
            return;
        }

        try
        {
            Clipboard.SetText(action.Value);
        }
        catch
        {
        }
    }

    /// <summary>В ссылке на карту сначала долгота, потом широта.</summary>
    private static string? CoordsUrl(string pair)
    {
        var parts = pair.Split(',');
        if (parts.Length != 2) return null;
        return $"https://yandex.ru/maps/?pt={parts[1]},{parts[0]}&z=16&l=map";
    }

    /// <summary>Снимает хвостовую пунктуацию, которая цепляется к ссылке в тексте.</summary>
    private static string Trim(string url) => url.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '»', '"', '\'');

    private static string Short(string url)
    {
        var clean = url.Replace("https://", "").Replace("http://", "");
        return clean.Length > 28 ? clean[..28] + "…" : clean;
    }
}
