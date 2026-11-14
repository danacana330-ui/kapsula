namespace Kapsula.Services;

/// <summary>Один таймер на всё приложение: панель можно закрыть, отсчёт продолжается.</summary>
public static class TimerService
{
    private const string FileName = "timer.json";
    private static DateTime? _end;
    private static DateTime? _firedAt;

    /// <summary>Время вышло и ещё не сброшено.</summary>
    public static event Action? Finished;

    public static bool IsRunning => _end is not null && DateTime.Now < _end.Value;

    public static bool IsFinished => _firedAt is not null;

    /// <summary>Показывать ли отсчёт на свёрнутой полосе вместо погоды.</summary>
    public static bool ShowOnStrip => IsRunning || (IsFinished && DateTime.Now - _firedAt!.Value < TimeSpan.FromSeconds(60));

    public static TimeSpan Remaining
    {
        get
        {
            if (_end is null) return TimeSpan.Zero;
            var left = _end.Value - DateTime.Now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public static void Start(TimeSpan span)
    {
        if (span <= TimeSpan.Zero) return;
        _end = DateTime.Now + span;
        _firedAt = null;
        Persist();
    }

    public static void Stop()
    {
        _end = null;
        _firedAt = null;
        Persist();
    }

    /// <summary>Подхватить отсчёт, который шёл до перезапуска приложения.</summary>
    public static void Restore()
    {
        var end = Store.Load<DateTime?>(FileName, null);
        if (end is null)
        {
            _end = null;
            return;
        }

        // Пропущенный таймер показываем, только если он истёк недавно.
        if (end.Value <= DateTime.Now && DateTime.Now - end.Value > TimeSpan.FromHours(12))
        {
            _end = null;
            Persist();
            return;
        }

        _end = end;
        _firedAt = null;
    }

    private static void Persist() => Store.Save(FileName, _end);

    /// <summary>Вызывается раз в секунду из общего тика окна.</summary>
    public static void Update()
    {
        if (_end is null || _firedAt is not null) return;
        if (DateTime.Now < _end.Value) return;

        _firedAt = DateTime.Now;
        _end = null;
        Persist();
        Finished?.Invoke();
    }

    public static string Text
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
