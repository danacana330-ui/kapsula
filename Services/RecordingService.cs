using System.Diagnostics;

namespace Kapsula.Services;

/// <summary>Запись экрана через ffmpeg (gdigrab). Звук системы — отдельный шаг, тут только видео.</summary>
public sealed class RecordingService
{
    private Process? _process;

    public bool IsRecording => _process is { HasExited: false };

    public string? LastFile { get; private set; }

    public string Start(string folder)
    {
        if (IsRecording) return LastFile ?? "";

        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"запись_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

        var args = "-y -f gdigrab -framerate 30 -i desktop " +
                   "-c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p " +
                   $"\"{file}\"";

        var info = new ProcessStartInfo("ffmpeg")
        {
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            // stderr не перенаправляем: непрочитанный поток переполняется и ffmpeg зависает
        };

        _process = Process.Start(info);
        LastFile = file;
        return file;
    }

    public async Task StopAsync()
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited)
            {
                await _process.StandardInput.WriteLineAsync("q");
                await _process.StandardInput.FlushAsync();
                await _process.WaitForExitAsync(new CancellationTokenSource(8000).Token);
            }
        }
        catch
        {
            try
            {
                if (!_process.HasExited) _process.Kill(true);
            }
            catch
            {
            }
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
