using System.Text.RegularExpressions;

namespace Kapsula.Services;

/// <summary>
/// Простые команды, которые капсула понимает без модели: таймер, скриншот, тема.
/// Работают офлайн и мгновенно — мини-модель 1.5B такого не умеет вовсе.
/// </summary>
public static class QuickCommands
{
    private static readonly Regex TimerRx = new(
        @"(?:таймер|напомни|напомнить)\D{0,12}?(\d+)\s*(минут|мин|час|ч|секунд|сек)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClockRx = new(
        @"(?:таймер|напомни|напомнить)\D{0,12}?(\d{1,2})[:.](\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Пробует выполнить команду. handled=false — обычный вопрос, идём в модель.</summary>
    public static bool TryHandle(string text, out string reply)
    {
        reply = "";
        var lowered = text.Trim().ToLowerInvariant();
        if (lowered.Length == 0) return false;

        if (ClockRx.Match(lowered) is { Success: true } clock &&
            int.TryParse(clock.Groups[1].Value, out var hour) &&
            int.TryParse(clock.Groups[2].Value, out var minute) &&
            hour is >= 0 and < 24 && minute is >= 0 and < 60)
        {
            var now = DateTime.Now;
            var target = now.Date.AddHours(hour).AddMinutes(minute);
            if (target <= now) target = target.AddDays(1);
            TimerService.Start(target - now);
            reply = $"Таймер до {hour:00}:{minute:00} поставлен.";
            return true;
        }

        if (TimerRx.Match(lowered) is { Success: true } match &&
            int.TryParse(match.Groups[1].Value, out var amount) && amount > 0)
        {
            var unit = match.Groups[2].Value;
            var span = unit switch
            {
                "час" or "ч" => TimeSpan.FromHours(amount),
                "секунд" or "сек" => TimeSpan.FromSeconds(amount),
                _ => TimeSpan.FromMinutes(amount),
            };
            TimerService.Start(span);
            reply = $"Таймер на {Describe(span)} поставлен.";
            return true;
        }

        if (lowered.Contains("скриншот") || lowered.Contains("снимок экрана") || lowered.Contains("сделай снимок"))
        {
            MainWindow.Instance?.TakeScreenshot();
            reply = "Открываю снимок экрана — выдели область.";
            return true;
        }

        if (lowered.Contains("светл") && lowered.Contains("тем"))
        {
            MainWindow.Instance?.SetLightTheme(true);
            reply = "Включил светлую тему.";
            return true;
        }

        if (lowered.Contains("тёмн") || lowered.Contains("темн"))
        {
            MainWindow.Instance?.SetLightTheme(false);
            reply = "Включил тёмную тему.";
            return true;
        }

        return false;
    }

    private static string Describe(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours} ч"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} мин" : $"{(int)span.TotalSeconds} с";
}
