namespace Kapsula.Services;

/// <summary>Один отсчёт: их может быть несколько одновременно.</summary>
public sealed class TimerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime End { get; set; }
    public string Label { get; set; } = "";

    public TimeSpan Remaining
    {
        get
        {
            var left = End - DateTime.Now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public string Text
    {
        get
        {
            var left = Remaining;
            return left.TotalHours >= 1
                ? $"{(int)left.TotalHours}:{left.Minutes:00}:{left.Seconds:00}"
                : $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        }
    }
}

/// <summary>Таймеры на всё приложение: панель можно закрыть, отсчёт продолжается.</summary>
public static class TimerService
{
    private const string FileName = "timers.json";
    private const string OldFileName = "timer.json";

    private static readonly List<TimerEntry> _items = new();
    private static DateTime? _firedAt;

    /// <summary>Время вышло у одного из таймеров и ещё не сброшено.</summary>
    public static event Action? Finished;

    public static IReadOnlyList<TimerEntry> Items => _items;

    public static bool IsRunning => _items.Count > 0;

    public static bool IsFinished => _firedAt is not null;

    /// <summary>Показывать ли отсчёт на свёрнутой полосе вместо погоды.</summary>
    public static bool ShowOnStrip => IsRunning || (IsFinished && DateTime.Now - _firedAt!.Value < TimeSpan.FromSeconds(60));

    /// <summary>Ближайший из идущих таймеров.</summary>
    public static TimerEntry? Nearest => _items.OrderBy(i => i.End).FirstOrDefault();

    public static TimeSpan Remaining => Nearest?.Remaining ?? TimeSpan.Zero;

    public static void Start(TimeSpan span, string? label = null)
    {
        if (span <= TimeSpan.Zero) return;
        _items.Add(new TimerEntry
        {
            End = DateTime.Now + span,
            Label = string.IsNullOrWhiteSpace(label) ? $"на {Describe(span)}" : label!,
        });
        _firedAt = null;
        Persist();
    }

    public static void Stop()
    {
        _items.Clear();
        _firedAt = null;
        Persist();
    }

    public static void Stop(string id)
    {
        _items.RemoveAll(i => i.Id == id);
        if (_items.Count == 0) _firedAt = null;
        Persist();
    }

    /// <summary>Подхватить отсчёты, которые шли до перезапуска приложения.</summary>
    public static void Restore()
    {
        _items.Clear();
        foreach (var entry in Store.Load(FileName, new List<TimerEntry>()))
        {
            // пропущенный таймер показываем, только если он истёк недавно
            if (entry.End <= DateTime.Now && DateTime.Now - entry.End > TimeSpan.FromHours(12)) continue;
            _items.Add(entry);
        }

        if (_items.Count == 0)
        {
            // перенос одиночного таймера из версии до 2026-11-16
            var old = Store.Load<DateTime?>(OldFileName, null);
            if (old is not null && old.Value > DateTime.Now.AddHours(-12))
                _items.Add(new TimerEntry { End = old.Value, Label = "таймер" });
        }

        Persist();
    }

    private static void Persist() => Store.Save(FileName, _items.ToList());

    /// <summary>Вызывается раз в секунду из общего тика окна.</summary>
    public static void Update()
    {
        if (_items.Count == 0) return;
        var expired = _items.Where(i => DateTime.Now >= i.End).ToList();
        if (expired.Count == 0) return;

        foreach (var entry in expired) _items.Remove(entry);
        _firedAt = DateTime.Now;
        Persist();
        Finished?.Invoke();
    }

    public static string Text
    {
        get
        {
            var nearest = Nearest;
            return nearest is null ? "00:00" : nearest.Text;
        }
    }

    private static string Describe(TimeSpan span) => span.TotalHours >= 1
        ? $"{(int)span.TotalHours} ч"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} мин" : $"{(int)span.TotalSeconds} с";
}
