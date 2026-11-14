using System.Diagnostics;
using Microsoft.Win32;
using Windows.Devices.Radios;

namespace Kapsula.Services;

/// <summary>Быстрые системные переключатели: Wi-Fi, Bluetooth, светлая/тёмная тема, загрузки.</summary>
public static class SystemToggles
{
    public static async Task<bool?> RadioStateAsync(RadioKind kind)
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var radio = radios.FirstOrDefault(r => r.Kind == kind);
            if (radio is null) return null;
            return radio.State == RadioState.On;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool?> ToggleRadioAsync(RadioKind kind)
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var radio = radios.FirstOrDefault(r => r.Kind == kind);
            if (radio is null) return null;
            var target = radio.State == RadioState.On ? RadioState.Off : RadioState.On;
            var result = await radio.SetStateAsync(target);
            return result == RadioAccessStatus.Allowed ? target == RadioState.On : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
        }
        catch
        {
            return false;
        }
    }

    public static void SetTheme(bool light)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
            key?.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
            key?.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }

    public static void LockScreen()
    {
        try
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation") { CreateNoWindow = true });
        }
        catch
        {
        }
    }

    public static void Sleep()
    {
        try
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { CreateNoWindow = true });
        }
        catch
        {
        }
    }

    public static void Reboot() => Power("shutdown", "/r /t 0");

    public static void Shutdown() => Power("shutdown", "/s /t 0");

    private static void Power(string file, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false });
        }
        catch
        {
        }
    }

    public static int ActiveDownloads()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(dir)) return 0;
            return Directory.EnumerateFiles(dir)
                .Count(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".crdownload" or ".part" or ".download" or ".opdownload";
                });
        }
        catch
        {
            return 0;
        }
    }
}

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Kapsula";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return;
            if (enabled)
                key.SetValue(Name, $"\"{Process.GetCurrentProcess().MainModule?.FileName}\"");
            else
                key.DeleteValue(Name, false);
        }
        catch
        {
        }
    }
}
