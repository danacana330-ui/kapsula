using System.Diagnostics;
using NAudio.Wave;

namespace Kapsula.Services;

/// <summary>Запись экрана через ffmpeg (gdigrab) со звуком системы через WASAPI loopback.</summary>
public sealed class RecordingService
{
    private Process? _process;
    private WasapiLoopbackCapture? _audio;
    private WaveFileWriter? _audioWriter;
    private string? _audioFile;

    public bool IsRecording => _process is { HasExited: false };

    public string? LastFile { get; private set; }

    public string Start(string folder, bool withAudio = true)
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
        _audioFile = null;
        if (withAudio) StartAudio(file);
        return file;
    }

    private void StartAudio(string videoFile)
    {
        var file = Path.ChangeExtension(videoFile, ".wav");
        try
        {
            var capture = new WasapiLoopbackCapture();
            var writer = new WaveFileWriter(file, capture.WaveFormat);
            capture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded <= 0) return;
                try
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch
                {
                    // диск кончился или файл закрыт — просто перестаём писать звук
                }
            };
            capture.StartRecording();

            // поля заполняем только после удачного старта, иначе уборка упадёт на полпути
            _audio = capture;
            _audioWriter = writer;
            _audioFile = file;
        }
        catch (Exception ex)
        {
            // нет звукового устройства или loopback недоступен — пишем только видео
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "kapsula_err.txt"),
                    "звук не записался: " + ex.Message + "\n---\n");
            }
            catch
            {
            }
        }
    }

    private void StopAudio()
    {
        var capture = _audio;
        var writer = _audioWriter;
        _audio = null;
        _audioWriter = null;

        if (capture is null) return;

        try
        {
            using var done = new ManualResetEventSlim(false);
            capture.RecordingStopped += (_, _) => done.Set();
            capture.StopRecording();
            done.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
        }

        try
        {
            capture.Dispose();
        }
        catch
        {
        }

        try
        {
            writer?.Dispose();
        }
        catch
        {
        }
    }

    public async Task StopAsync()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            StopAudio();
            await MuxAsync();
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                var input = process.StandardInput;
                if (input is not null)
                {
                    await input.WriteLineAsync("q");
                    await input.FlushAsync();
                }
                await process.WaitForExitAsync(new CancellationTokenSource(8000).Token);
            }
        }
        catch
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch
            {
            }
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }

        StopAudio();
        await MuxAsync();
    }

    /// <summary>Приклеить записанный звук к видео; если звука нет — оставить видео как есть.</summary>
    private async Task MuxAsync()
    {
        var video = LastFile;
        var audio = _audioFile;
        _audioFile = null;
        if (video is null || audio is null) return;

        try
        {
            if (!File.Exists(audio) || new FileInfo(audio).Length < 1024)
            {
                File.Delete(audio);
                return;
            }

            var mixed = Path.ChangeExtension(video, ".mix.mp4");
            var info = new ProcessStartInfo("ffmpeg")
            {
                Arguments = $"-y -i \"{video}\" -i \"{audio}\" -c:v copy -c:a aac -b:a 160k -shortest \"{mixed}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using var p = Process.Start(info);
            if (p is null) return;
            // потоки вычитываем, иначе ffmpeg может зависнуть на переполненном буфере
            var drain = Task.WhenAll(p.StandardError.ReadToEndAsync(), p.StandardOutput.ReadToEndAsync());
            await p.WaitForExitAsync(new CancellationTokenSource(120000).Token);
            await drain;

            if (p.ExitCode == 0 && File.Exists(mixed))
            {
                File.Move(mixed, video, true);
                File.Delete(audio);
            }
            else
            {
                if (File.Exists(mixed)) File.Delete(mixed);
                File.Delete(audio);
            }
        }
        catch
        {
            // не удалось приклеить звук — видео уже сохранено, ничего не ломаем
            try
            {
                if (audio is not null && File.Exists(audio)) File.Delete(audio);
            }
            catch
            {
            }
        }
    }
}
