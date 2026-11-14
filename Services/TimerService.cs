namespace Kapsula.Services;

/// <summary>Один таймер на всё приложение: панель можно закрыть, отсчёт продолжается.</summary>
public static class TimerService
{
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
    }

    public static void Stop()
    {
        _end = null;
        _firedAt = null;
    }

    /// <summary>Вызывается раз в секунду из общего тика окна.</summary>
    public static void Update()
    {
        if (_end is null || _firedAt is not null) return;
        if (DateTime.Now < _end.Value) return;

        _firedAt = DateTime.Now;
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
