using System.Globalization;
using System.Text.RegularExpressions;
using Windows.Devices.Radios;

namespace Kapsula.Services;

public readonly record struct CommandResult(bool Handled, string Reply)
{
    public static CommandResult Not => new(false, "");
    public static CommandResult Done(string reply) => new(true, reply);
}

/// <summary>
/// Команды капсулы, которые выполняются без модели: мгновенно, офлайн и одинаково
/// для любого источника. Мини-модель 1.5B такого не умеет вовсе, а облачная
/// отвечает текстом, поэтому действия разбираем здесь.
/// </summary>
public static class QuickCommands
{
    public const string HelpText =
        "Умею по капсуле почти всё. Скажи или напиши:\n" +
        "· «таймер 15 минут», «таймер до 18:30»\n" +
        "· «напомни завтра в 9 позвонить маме», «напомни через час про чай»\n" +
        "· «запомни купить фильтр», «запиши в заметки …»\n" +
        "· «сводка», «погода», «курс доллара», «что в буфере»\n" +
        "· «громче», «тише», «громкость 40», «без звука»\n" +
        "· «яркость 60», «ярче», «тусклее»\n" +
        "· «включи wi-fi», «выключи bluetooth», «тёмная тема»\n" +
        "· «скриншот», «запись экрана»\n" +
        "· «пауза», «следующий трек», «предыдущий трек»\n" +
        "· «открой заметки», «открой таймер», «открой настройки»\n" +
        "· «открой сайт ozon.ru», «сделай бэкап», «заблокируй экран», «свернись»";

    public static async Task<CommandResult> TryHandleAsync(string text)
    {
        var message = (text ?? "").Trim();
        if (message.Length == 0) return CommandResult.Not;
        var lower = message.ToLowerInvariant();
        var main = MainWindow.Instance;

        if (Has(lower, "что ты умеешь", "что умеешь", "твои возможности", "список команд", "команды", "помощь"))
            return CommandResult.Done(HelpText);

        if (Has(lower, "таймер") && TryTimer(lower, out var timerReply))
            return CommandResult.Done(timerReply);

        if (Regex.IsMatch(lower, @"(^|\s)напомн") && TryReminder(message, lower, out var remindReply))
            return CommandResult.Done(remindReply);

        if (Starts(lower, "запомни", "запиши", "добавь в заметки", "в заметки", "заметка"))
        {
            var what = Strip(message, "запомни", "запиши", "добавь в заметки", "в заметки", "заметка", "пожалуйста");
            what = what.Trim(' ', ':', '-', '—', ',', '.');
            if (what.Length > 0)
            {
                NotesStore.Add(what);
                return CommandResult.Done($"Записал в заметки: {what}");
            }
        }

        if (Has(lower, "сводк", "итоги дня", "что по дню", "как прошёл день", "как прошел день"))
        {
            main?.ShowSummary();
            return CommandResult.Done("Открываю сводку дня.");
        }

        if (Has(lower, "очисти буфер", "очистить буфер", "сотри буфер"))
        {
            ClipboardStore.Clear();
            return CommandResult.Done("Буфер очищен.");
        }

        if (Has(lower, "буфер"))
        {
            var items = ClipboardStore.Items.Take(3).Select(i => Short(i.Text, 80)).ToList();
            if (items.Count == 0) return CommandResult.Done("Буфер пуст.");
            return CommandResult.Done("В буфере сейчас:\n" + string.Join("\n", items.Select(i => "· " + i)));
        }

        if (Has(lower, "погод"))
        {
            if (main is null) return CommandResult.Done("Капсула ещё не открыта.");
            return CommandResult.Done(await main.CommandWeatherAsync());
        }

        if (Has(lower, "курс"))
        {
            var wanted = lower.Contains("доллар") ? "USD"
                : lower.Contains("евро") ? "EUR"
                : lower.Contains("юан") ? "CNY" : "";
            var rates = await new CurrencyService().GetAsync();
            if (rates.Count == 0) return CommandResult.Done("Курсы не пришли — похоже, нет сети.");
            var line = string.Join(", ", rates
                .Where(r => wanted.Length == 0 || r.Code == wanted)
                .Select(r => $"{r.Name} {r.Text} ₽"));
            return CommandResult.Done(line.Length == 0 ? "Такой валюты в сводке нет." : line);
        }

        if (TryVolume(lower, out var volumeReply, out var volumeHandled))
            return volumeHandled ? CommandResult.Done(volumeReply) : CommandResult.Not;

        if (TryBrightness(lower, out var brightReply))
            return CommandResult.Done(brightReply);

        if (Has(lower, "светл") && Has(lower, "тему", "тема", "темную", "тёмную"))
        {
            main?.SetLightTheme(true);
            return CommandResult.Done("Включил светлую тему.");
        }

        if (Has(lower, "тёмн", "темн") && Has(lower, "тему", "тема", "темную", "тёмную"))
        {
            main?.SetLightTheme(false);
            return CommandResult.Done("Включил тёмную тему.");
        }

        if (Has(lower, "wi-fi", "wi fi", "вайфай", "вай фай", "вай-фай"))
            return CommandResult.Done(await RadioAsync(RadioKind.WiFi, lower, "Wi-Fi"));

        if (Has(lower, "bluetooth", "блютус", "блюпуп"))
            return CommandResult.Done(await RadioAsync(RadioKind.Bluetooth, lower, "Bluetooth"));

        if (Has(lower, "скриншот", "снимок экрана", "сделай снимок"))
        {
            main?.TakeScreenshot();
            return CommandResult.Done("Открываю снимок экрана — выдели область.");
        }

        if (Has(lower, "запись экрана", "запиши экран", "начать запись", "останови запись", "стоп запись"))
        {
            if (main is null) return CommandResult.Done("Капсула ещё не открыта.");
            return CommandResult.Done(await main.CommandRecordAsync());
        }

        if (Has(lower, "следующий трек", "след трек", "переключи трек"))
        {
            if (main is null) return CommandResult.Done("Капсула ещё не открыта.");
            return CommandResult.Done(await main.CommandMusicAsync("next"));
        }

        if (Has(lower, "предыдущий трек", "назад трек"))
        {
            if (main is null) return CommandResult.Done("Капсула ещё не открыта.");
            return CommandResult.Done(await main.CommandMusicAsync("prev"));
        }

        if (Has(lower, "пауза", "поставь на паузу", "продолжи музыку", "включи музыку", "играй"))
        {
            if (main is null) return CommandResult.Done("Капсула ещё не открыта.");
            return CommandResult.Done(await main.CommandMusicAsync("toggle"));
        }

        if (Has(lower, "бэкап", "резервную копию", "резервная копия"))
        {
            var (path, error) = BackupService.Export(BackupService.DefaultPath());
            return CommandResult.Done(error is null && path is not null
                ? $"Копия сохранена: {path}"
                : $"Копию сделать не вышло: {error}");
        }

        if (Has(lower, "заблокируй"))
        {
            SystemToggles.LockScreen();
            return CommandResult.Done("Экран заблокирован.");
        }

        if (Has(lower, "свернись", "закрой капсулу", "скрой капсулу"))
        {
            main?.Collapse();
            return CommandResult.Done("Свернулся.");
        }

        if (Starts(lower, "открой"))
        {
            var open = TryOpen(message, lower);
            if (open is not null) return CommandResult.Done(open);
        }

        return CommandResult.Not;
    }

    // ── таймер ───────────────────────────────────────────────────────────────

    private static bool TryTimer(string lower, out string reply)
    {
        reply = "";
        var time = ReminderTime.TimeRx.Match(lower);
        if (time.Success &&
            int.TryParse(time.Groups[1].Value, out var hour) &&
            int.TryParse(time.Groups[2].Value, out var minute) &&
            hour is >= 0 and < 24 && minute is >= 0 and < 60)
        {
            var now = DateTime.Now;
            var target = now.Date.AddHours(hour).AddMinutes(minute);
            if (target <= now) target = target.AddDays(1);
            TimerService.Start(target - now);
            reply = $"Таймер до {hour:00}:{minute:00} поставлен.";
            return true;
        }

        var rel = ReminderTime.RelativeRx.Match(lower);
        if (rel.Success && int.TryParse(rel.Groups[1].Value, out var amount) && amount > 0)
        {
            var span = ReminderTime.SpanOf(amount, rel.Groups[2].Value);
            TimerService.Start(span);
            reply = $"Таймер на {Describe(span)} поставлен.";
            return true;
        }

        return false;
    }

    // ── напоминания ──────────────────────────────────────────────────────────

    private static bool TryReminder(string message, string lower, out string reply)
    {
        reply = "";
        var hasWhen = ReminderTime.TryParse(lower, out var when);
        if (!hasWhen) when = DateTime.Now.AddHours(1);

        var what = ReminderTime.StripTime(message);
        what = Strip(what, "напомни", "напомнить", "мне", "пожалуйста", "о том что", "что");
        what = what.Trim(' ', ':', '-', '—', ',', '.');
        if (what.Length == 0) what = "напоминание";

        var note = NotesStore.Add(what);
        NotesStore.Remind(note, when);
        reply = hasWhen
            ? $"Напомню {ReminderTime.Human(when)}: {what}"
            : $"Время не расслышал — напомню {ReminderTime.Human(when)}: {what}";
        return true;
    }

    // ── мелочи ───────────────────────────────────────────────────────────────

    private static bool TryVolume(string lower, out string reply, out bool handled)
    {
        reply = "";
        handled = false;

        var number = Regex.Match(lower, @"громкость\s+(\d{1,3})");
        if (number.Success && int.TryParse(number.Groups[1].Value, out var percent))
        {
            percent = Math.Clamp(percent, 0, 100);
            VolumeService.Set(percent / 100f);
            handled = true;
            reply = $"Громкость {percent}%.";
            return true;
        }

        if (Has(lower, "без звука", "выключи звук", "замолчи", "мьют", "mute"))
        {
            if (!VolumeService.IsMuted()) VolumeService.ToggleMute();
            handled = true;
            reply = "Звук выключен.";
            return true;
        }

        if (Has(lower, "включи звук", "верни звук"))
        {
            if (VolumeService.IsMuted()) VolumeService.ToggleMute();
            handled = true;
            reply = "Звук включён.";
            return true;
        }

        if (Has(lower, "громче", "прибавь звук", "погромче"))
        {
            var next = Math.Clamp(VolumeService.Get() + 0.1f, 0f, 1f);
            VolumeService.Set(next);
            handled = true;
            reply = $"Громкость {(int)Math.Round(next * 100)}%.";
            return true;
        }

        if (Has(lower, "тише", "убавь звук", "потише"))
        {
            var next = Math.Clamp(VolumeService.Get() - 0.1f, 0f, 1f);
            VolumeService.Set(next);
            handled = true;
            reply = $"Громкость {(int)Math.Round(next * 100)}%.";
            return true;
        }

        return false;
    }

    private static bool TryBrightness(string lower, out string reply)
    {
        reply = "";

        var number = Regex.Match(lower, @"яркость\s+(\d{1,3})");
        if (number.Success && int.TryParse(number.Groups[1].Value, out var percent))
        {
            percent = Math.Clamp(percent, 5, 100);
            reply = BrightnessService.Set(percent)
                ? $"Яркость {percent}%."
                : "Эта видеокарта не даёт менять яркость из приложений.";
            return true;
        }

        if (Has(lower, "ярче", "поярче", "прибавь яркость"))
        {
            var current = BrightnessService.Get() ?? 60;
            var next = Math.Clamp(current + 10, 5, 100);
            reply = BrightnessService.Set(next) ? $"Яркость {next}%." : "Яркость менять не выходит.";
            return true;
        }

        if (Has(lower, "тусклее", "потемнее экран", "убавь яркость", "темнее экран"))
        {
            var current = BrightnessService.Get() ?? 60;
            var next = Math.Clamp(current - 10, 5, 100);
            reply = BrightnessService.Set(next) ? $"Яркость {next}%." : "Яркость менять не выходит.";
            return true;
        }

        return false;
    }

    private static async Task<string> RadioAsync(RadioKind kind, string lower, string name)
    {
        var state = await SystemToggles.RadioStateAsync(kind);
        if (state is null) return $"Не нашёл адаптер {name}.";

        var off = Has(lower, "выключи", "отключи", "выруби");
        var on = Has(lower, "включи", "вруби");
        if (!off && !on) return state.Value ? $"{name} включён." : $"{name} выключен.";

        if (state.Value == on) return $"{name} уже {(on ? "включён" : "выключен")}.";

        var result = await SystemToggles.ToggleRadioAsync(kind);
        return result is null
            ? $"Не смог переключить {name} — система не разрешила."
            : $"{name} {(result.Value ? "включён" : "выключен")}.";
    }

    private static string? TryOpen(string message, string lower)
    {
        if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains(".ru") ||
            lower.Contains(".com") || lower.Contains(".рф"))
        {
            var match = Regex.Match(message, @"(https?://\S+|[\w.-]+\.(ru|com|рф|net|org)\S*)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var url = match.Value;
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                    { UseShellExecute = true });
                    return $"Открываю {url}";
                }
                catch
                {
                    return "Браузер открыть не вышло.";
                }
            }
        }

        var map = new (string[] Words, string Tag, string Name)[]
        {
            (new[] { "заметк", "записи" }, "notes", "заметки"),
            (new[] { "таймер" }, "timer", "таймер"),
            (new[] { "погод" }, "weather", "погоду"),
            (new[] { "буфер" }, "buffer", "буфер"),
            (new[] { "сводк" }, "summary", "сводку"),
            (new[] { "полк" }, "shelf", "полку"),
            (new[] { "настройк" }, "settings", "настройки"),
            (new[] { "ии", "чат" }, "ai", "ИИ"),
        };

        foreach (var (words, tag, name) in map)
            if (words.Any(lower.Contains))
            {
                MainWindow.Instance?.OpenTile(tag);
                return $"Открываю {name}.";
            }

        return null;
    }

    private static string Describe(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours} ч"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} мин" : $"{(int)span.TotalSeconds} с";

    private static string Short(string text, int limit)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= limit ? text : text[..limit] + "…";
    }

    private static bool Has(string lower, params string[] words) => words.Any(lower.Contains);

    private static bool Starts(string lower, params string[] words) =>
        words.Any(w => lower.StartsWith(w, StringComparison.Ordinal));

    private static string Strip(string text, params string[] words)
    {
        var result = text;
        foreach (var word in words)
            result = Regex.Replace(result, @"(?i)\b" + Regex.Escape(word) + @"\b", " ");
        return Regex.Replace(result, @"\s{2,}", " ").Trim();
    }
}
