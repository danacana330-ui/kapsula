using System.IO;
using System.Runtime.InteropServices;

namespace Kapsula.Services;

/// <summary>Состояние машины: память, процессор, диск, батарея — без сторонних библиотек.</summary>
public static class SysInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX info);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    private static ulong _prevIdle;
    private static ulong _prevBusy;

    public static (double Used, double Total) Memory()
    {
        var info = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref info)) return (0, 0);
        var total = info.ullTotalPhys / 1073741824.0;
        return (total - info.ullAvailPhys / 1073741824.0, total);
    }

    /// <summary>Загрузка процессора в процентах: разница двух замеров подряд.</summary>
    public static int CpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;

        var idleTicks = ((ulong)idle.High << 32) | idle.Low;
        var busyTicks = (((ulong)kernel.High << 32) | kernel.Low) + (((ulong)user.High << 32) | user.Low);

        if (_prevBusy == 0)
        {
            _prevIdle = idleTicks;
            _prevBusy = busyTicks;
            return 0;
        }

        var idleDelta = idleTicks - _prevIdle;
        var busyDelta = busyTicks - _prevBusy;
        _prevIdle = idleTicks;
        _prevBusy = busyTicks;

        var total = idleDelta + busyDelta;
        if (total == 0) return 0;
        return (int)Math.Round(100.0 * busyDelta / total);
    }

    public static (double Free, double Total) Disk(string root = "C:\\")
    {
        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady) return (0, 0);
            return (drive.AvailableFreeSpace / 1073741824.0, drive.TotalSize / 1073741824.0);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>Заряд в процентах, на зарядке ли, есть ли вообще батарея.</summary>
    public static (int Percent, bool Charging, bool HasBattery) Battery()
    {
        if (!GetSystemPowerStatus(out var status)) return (0, false, false);
        var hasBattery = status.BatteryFlag != 128 && status.BatteryFlag != 255;
        var percent = status.BatteryLifePercent == 255 ? 0 : status.BatteryLifePercent;
        return (percent, status.ACLineStatus == 1, hasBattery);
    }
}
