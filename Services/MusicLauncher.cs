using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Kapsula.Services;

/// <summary>
/// Запуск плеера, когда его нет в системе. Окно сразу уводим вниз: капсула должна
/// включать музыку, а не выкидывать человека из его работы.
/// </summary>
public static class MusicLauncher
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    private const int SwMinimize = 6;

    private static readonly (string Process, string[] Paths)[] Known =
    {
        ("YandexMusic", new[]
        {
            @"%LOCALAPPDATA%\Programs\YandexMusic\YandexMusic.exe",
            @"%LOCALAPPDATA%\YandexMusic\YandexMusic.exe",
            @"%PROGRAMFILES%\Yandex Music\YandexMusic.exe",
        }),
        ("Spotify", new[] { @"%APPDATA%\Spotify\Spotify.exe" }),
        ("AIMP", new[] { @"%PROGRAMFILES%\AIMP\AIMP.exe", @"%PROGRAMFILES(X86)%\AIMP\AIMP.exe" }),
        ("vlc", new[] { @"%PROGRAMFILES%\VideoLAN\VLC\vlc.exe", @"%PROGRAMFILES(X86)%\VideoLAN\VLC\vlc.exe" }),
    };

    /// <summary>Плеер уже работает — второй экземпляр не нужен, ждём медиасессию.</summary>
    public static bool IsRunning()
    {
        foreach (var (name, _) in Known)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0) return true;
            }
            catch
            {
            }
        }
        return false;
    }

    /// <summary>Запустить последний известный плеер: сначала по идентификатору приложения, потом по путям.</summary>
    public static bool Launch(string aumid)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(aumid) && aumid.Contains('!'))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{aumid}")
                {
                    UseShellExecute = true,
                });
                return true;
            }

            foreach (var (_, paths) in Known)
            {
                foreach (var raw in paths)
                {
                    var path = Environment.ExpandEnvironmentVariables(raw);
                    if (!File.Exists(path)) continue;
                    Process.Start(new ProcessStartInfo(path)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized,
                    });
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    /// <summary>
    /// Электронные плееры флаг «свёрнуто» при запуске игнорируют, поэтому ищем их окна
    /// несколько секунд и сворачиваем руками.
    /// </summary>
    public static async Task HideWindowsAsync(int seconds = 4)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            var found = false;
            foreach (var (name, _) in Known)
            {
                Process[] list;
                try
                {
                    list = Process.GetProcessesByName(name);
                }
                catch
                {
                    continue;
                }

                foreach (var process in list)
                {
                    try
                    {
                        var handle = process.MainWindowHandle;
                        if (handle == IntPtr.Zero) continue;
                        ShowWindow(handle, SwMinimize);
                        found = true;
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            if (found) return;
            await Task.Delay(300);
        }
    }
}
