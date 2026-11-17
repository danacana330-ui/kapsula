using System.Management;

namespace Kapsula.Services;

/// <summary>
/// Яркость экрана через WMI (`root\wmi`). Dxva2 на панели этого ноутбука яркость не отдаёт,
/// поэтому единственный рабочий путь — WmiMonitorBrightness / WmiMonitorBrightnessMethods.
/// </summary>
public static class BrightnessService
{
    private const string Scope = @"root\wmi";
    private static ManagementEventWatcher? _watcher;

    public static int? Get()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope,
                "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                    return Convert.ToInt32(item["CurrentBrightness"]);
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>Ставит яркость 0–100. Возвращает false, если система не даёт менять.</summary>
    public static bool Set(int percent)
    {
        var value = (byte)Math.Clamp(percent, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope,
                "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    item.InvokeMethod("WmiSetBrightness", new object[] { (ushort)1, value });
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>Яркость поменяли с клавиш, из Windows или из нашей панели — сообщаем новое значение.</summary>
    public static void Watch(Action<int> changed)
    {
        if (_watcher is not null) return;

        try
        {
            _watcher = new ManagementEventWatcher(Scope, "SELECT * FROM WmiMonitorBrightnessEvent");
            _watcher.EventArrived += (_, e) =>
            {
                try
                {
                    if (e.NewEvent["Brightness"] is { } raw) changed(Convert.ToInt32(raw));
                }
                catch
                {
                }
            };
            _watcher.Start();
        }
        catch
        {
            _watcher = null;
        }
    }
}
