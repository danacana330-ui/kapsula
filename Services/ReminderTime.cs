using System.Text.RegularExpressions;

namespace Kapsula.Services;

/// <summary>
/// Разбор «когда напомнить» из обычной фразы: «через час», «завтра в 9»,
/// «18.11 в 14:30», «в 20:00». Без зависимостей от интерфейса — чтобы
/// проверять отдельно от приложения.
/// </summary>
public static class ReminderTime
{
    public static readonly Regex RelativeRx = new(
        @"через\s+(\d+)\s*(минут\w*|мин|час\w*|ч|секунд\w*|сек)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex RelativeNoNumRx = new(
        @"через\s+(полчаса|пол-часа|час|минуту|секунду)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex DateRx = new(
        @"\b(\d{1,2})\.(\d{1,2})\b",
        RegexOptions.Compiled);

    public static readonly Regex TimeRx = new(
        @"(?:\bв\s+)?\b(\d{1,2})[:.](\d{2})\b",
        RegexOptions.Compiled);

    public static readonly Regex HourRx = new(
        @"\bв\s+(\d{1,2})(?:\s*(?:час\w*|ч))?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string text, out DateTime when)
    {
        when = default;
        var lower = text.ToLowerInvariant();
        var now = DateTime.Now;

        var rel = RelativeRx.Match(lower);
        if (rel.Success && int.TryParse(rel.Groups[1].Value, out var amount) && amount > 0)
        {
            when = now + SpanOf(amount, rel.Groups[2].Value);
            return true;
        }

        var one = RelativeNoNumRx.Match(lower);
        if (one.Success)
        {
            when = now + SpanOf(1, one.Groups[1].Value);
            return true;
        }

        var day = now.Date;
        var tomorrow = lower.Contains("завтра") || lower.Contains("послезавтра");
        if (lower.Contains("послезавтра")) day = day.AddDays(2);
        else if (lower.Contains("завтра")) day = day.AddDays(1);

        var hasDate = false;
        var date = DateRx.Match(lower);
        if (date.Success &&
            int.TryParse(date.Groups[1].Value, out var dd) &&
            int.TryParse(date.Groups[2].Value, out var mm) &&
            dd is >= 1 and <= 31 && mm is >= 1 and <= 12)
        {
            try
            {
                day = new DateTime(now.Year, mm, dd);
                if (day < now.Date) day = day.AddYears(1);
                hasDate = true;
            }
            catch
            {
                hasDate = false;
            }
        }

        var rest = hasDate ? lower.Replace(date.Value, " ") : lower;

        int? hour = null;
        int minute = 0;

        var clock = TimeRx.Match(rest);
        if (clock.Success &&
            int.TryParse(clock.Groups[1].Value, out var h1) &&
            int.TryParse(clock.Groups[2].Value, out var m1) &&
            h1 is >= 0 and < 24 && m1 is >= 0 and < 60)
        {
            hour = h1;
            minute = m1;
        }
        else
        {
            var hourOnly = HourRx.Match(rest);
            if (hourOnly.Success &&
                int.TryParse(hourOnly.Groups[1].Value, out var h2) &&
                h2 is >= 0 and < 24)
            {
                hour = h2;
            }
        }

        if (hour is null) return false;

        when = day.AddHours(hour.Value).AddMinutes(minute);
        if (!hasDate && !tomorrow && when <= now) when = when.AddDays(1);
        return true;
    }

    /// <summary>Убирает из фразы всё, что относится ко времени.</summary>
    public static string StripTime(string text)
    {
        var cleaned = RelativeRx.Replace(text, " ");
        cleaned = RelativeNoNumRx.Replace(cleaned, " ");
        cleaned = DateRx.Replace(cleaned, " ");
        cleaned = TimeRx.Replace(cleaned, " ");
        cleaned = HourRx.Replace(cleaned, " ");
        cleaned = Regex.Replace(cleaned, @"\b(завтра|послезавтра|сегодня)\b", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"(^|\s)во?\s+", " ", RegexOptions.IgnoreCase);
        return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
    }

    public static TimeSpan SpanOf(int amount, string unit)
    {
        var value = unit.ToLowerInvariant();
        if (value.StartsWith("полчас") || value.StartsWith("пол-час")) return TimeSpan.FromMinutes(30);
        if (value.StartsWith("час") || value == "ч") return TimeSpan.FromHours(amount);
        if (value.StartsWith("сек")) return TimeSpan.FromSeconds(amount);
        return TimeSpan.FromMinutes(amount);
    }

    public static string Human(DateTime when)
    {
        var now = DateTime.Now;
        var day = when.Date == now.Date ? "сегодня"
            : when.Date == now.Date.AddDays(1) ? "завтра"
            : when.Date == now.Date.AddDays(2) ? "послезавтра"
            : when.ToString("dd.MM");
        return day == "сегодня" ? $"в {when:HH:mm}" : $"в {when:HH:mm} {day}";
    }
}