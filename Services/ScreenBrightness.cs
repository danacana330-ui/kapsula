using System.Runtime.InteropServices;

namespace Kapsula.Services;

/// <summary>
/// Одна ручка яркости на все случаи: если панель отдаёт подсветку (WMI) — меняем подсветку,
/// если нет (на этом ноутбуке и WMI, и Dxva2 отказывают) — затемняем картинку гаммой.
/// </summary>
public static class ScreenBrightness
{
    private static bool? _hardware;

    /// <summary>Подсветка управляется по-настоящему, а не гаммой.</summary>
    public static bool IsHardware => _hardware ??= Probe();

    /// <summary>
    /// Проверяем не «не выдал ли ошибку», а реально ли поменялось значение: на этом ноутбуке
    /// WMI принимает вызов и молча ничего не делает.
    /// </summary>
    private static bool Probe()
    {
        var before = BrightnessService.Get();
        if (before is null) return false;

        var target = before.Value == 100 ? 99 : before.Value + 1;
        if (!BrightnessService.Set(target)) return false;

        var after = BrightnessService.Get();
        BrightnessService.Set(before.Value);
        return after is not null && Math.Abs(after.Value - target) <= 1;
    }

    public static int Get()
    {
        if (IsHardware) return BrightnessService.Get() ?? 100;
        return _gammaPercent;
    }

    public static void Set(int percent)
    {
        var value = Math.Clamp(percent, MinPercent, 100);
        if (IsHardware)
        {
            BrightnessService.Set(value);
            return;
        }

        ApplyGamma(value / 100.0);
        _gammaPercent = value;
    }

    /// <summary>Вернуть экран как было — при выходе, чтобы затемнение не осталось навсегда.</summary>
    public static void Restore()
    {
        if (IsHardware) return;
        ApplyGamma(1.0);
        _gammaPercent = 100;
    }

    public const int MinPercent = 20;

    private static int _gammaPercent = 100;

    private static void ApplyGamma(double factor)
    {
        var ramp = new ushort[256 * 3];
        for (var i = 0; i < 256; i++)
        {
            var v = (ushort)Math.Min(65535, Math.Round(i * 257 * factor));
            ramp[i] = v;
            ramp[256 + i] = v;
            ramp[512 + i] = v;
        }

        var dc = GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return;
        try
        {
            SetDeviceGammaRamp(dc, ramp);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);
}
