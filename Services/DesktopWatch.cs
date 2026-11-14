using System.Text;
using Microsoft.Win32;

namespace Kapsula.Services;

/// <summary>Наблюдение за рабочим столом: микрофон/камера, полноэкранные окна, бездействие.</summary>
public static class DesktopWatch
{
    private const string ConsentRoot =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public static bool MicInUse() => InUse("microphone");

    public static bool CameraInUse() => InUse("webcam");

    /// <summary>Идёт ли сейчас запись с микрофона/камеры хоть одним приложением.</summary>
    private static bool InUse(string kind)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey($@"{ConsentRoot}\{kind}");
            if (root is null) return false;
            return Scan(root, 0);
        }
        catch
        {
            return false;
        }
    }

    private static bool Scan(RegistryKey key, int depth)
    {
        if (IsActive(key)) return true;
        if (depth >= 2) return false;
        foreach (var name in key.GetSubKeyNames())
        {
            if (name is "NonPackaged")
            {
                using var np = key.OpenSubKey(name);
                if (np is null) continue;
                foreach (var app in np.GetSubKeyNames())
                {
                    using var appKey = np.OpenSubKey(app);
                    if (appKey is not null && IsActive(appKey)) return true;
                }
                continue;
            }

            using var child = key.OpenSubKey(name);
            if (child is not null && Scan(child, depth + 1)) return true;
        }
        return false;
    }

    private static bool IsActive(RegistryKey key)
    {
        var start = key.GetValue("LastUsedTimeStart");
        var stop = key.GetValue("LastUsedTimeStop");
        if (start is not long s || stop is not long e) return false;
        return s > 0 && e == 0;
    }

    /// <summary>Активное окно занимает весь монитор — видео, игра, полноэкранный браузер.</summary>
    public static bool ForegroundIsFullscreen(IntPtr own)
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == own) return false;
            if (NativeMethods.IsIconic(hwnd)) return false;

            var cls = new StringBuilder(64);
            NativeMethods.GetClassName(hwnd, cls, cls.Capacity);
            var name = cls.ToString();
            if (name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;

            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;

            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

            var m = info.rcMonitor;
            return rect.Left <= m.Left + 1 && rect.Top <= m.Top + 1
                   && rect.Right >= m.Right - 1 && rect.Bottom >= m.Bottom - 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Сколько миллисекунд пользователь не трогал мышь и клавиатуру.</summary>
    public static uint IdleMilliseconds()
    {
        try
        {
            var info = new NativeMethods.LASTINPUTINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
            };
            if (!NativeMethods.GetLastInputInfo(ref info)) return 0;
            return NativeMethods.GetTickCount() - info.dwTime;
        }
        catch
        {
            return 0;
        }
    }
}
