using System.Net.Http;

namespace Kapsula.Services;

public sealed record MiniModel(string Id, string Title, string Note, long Bytes, string[] Parts);

/// <summary>Мини-модели, которые капсула скачивает сама (файлы лежат в релизе на GitHub).</summary>
public static class MiniModels
{
    private const string Release = "https://github.com/danacana330-ui/kapsula/releases/download/models-v1/";

    public static readonly MiniModel Small = new(
        "qwen2.5-1.5b",
        "Qwen2.5 1.5B",
        "быстрая · 1,1 ГБ",
        1_117_320_736,
        new[] { "qwen2.5-1.5b-instruct-q4_k_m.gguf" });

    public static readonly MiniModel Medium = new(
        "qwen2.5-3b",
        "Qwen2.5 3B",
        "умнее · 2,0 ГБ",
        2_104_932_768,
        new[] { "qwen2.5-3b-instruct-q4_k_m.gguf.part1", "qwen2.5-3b-instruct-q4_k_m.gguf.part2" });

    public static readonly MiniModel[] All = { Small, Medium };

    public static string Url(string part) => Release + part;
}

public static class MiniModelStore
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(60) };

    public static string Dir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kapsula", "models");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string FilePath(MiniModel model) => Path.Combine(Dir, model.Id + ".gguf");

    public static bool IsInstalled(MiniModel model) =>
        File.Exists(FilePath(model)) && File.Exists(FilePath(model) + ".ok");

    public static async Task<bool> DownloadAsync(MiniModel model, Action<int> progress, CancellationToken token)
    {
        var target = FilePath(model);
        var temp = target + ".download";

        try
        {
            var total = model.Bytes > 0 ? model.Bytes : 1;
            long done = 0;

            await using (var output = File.Create(temp))
            {
                foreach (var part in model.Parts)
                {
                    using var response = await Http.GetAsync(MiniModels.Url(part),
                        HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();

                    await using var source = await response.Content.ReadAsStreamAsync(token);
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, token)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), token);
                        done += read;
                        progress((int)Math.Min(100, done * 100 / total));
                    }
                }
            }

            File.Move(temp, target, true);
            await File.WriteAllTextAsync(target + ".ok", DateTime.Now.ToString("s"), token);
            progress(100);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
            }
            return false;
        }
    }

    public static void Delete(MiniModel model)
    {
        try
        {
            var path = FilePath(model);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".ok")) File.Delete(path + ".ok");
        }
        catch
        {
        }
    }

    public static string SizeText(MiniModel model)
    {
        try
        {
            var path = FilePath(model);
            if (!File.Exists(path)) return model.Note;
            var mb = new FileInfo(path).Length / 1024.0 / 1024.0;
            return mb >= 1024 ? $"{mb / 1024:0.0} ГБ" : $"{mb:0} МБ";
        }
        catch
        {
            return model.Note;
        }
    }
}
