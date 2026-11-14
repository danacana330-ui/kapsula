using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kapsula.Services;

public sealed class UpdateInfo
{
    public string Version { get; init; } = "";
    public string Url { get; init; } = "";
    public long Size { get; init; }
    public string Sha256 { get; init; } = "";
}

/// <summary>Проверка новых версий на GitHub Releases и замена exe на месте.</summary>
public static class Updater
{
    private const string Api = "https://api.github.com/repos/danacana330-ui/kapsula/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kapsula");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    /// <summary>Последний релиз, если он новее текущей версии; иначе null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var response = await Http.GetAsync(Api);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() ?? "" : "";
            var version = ParseVersion(tag);
            if (version is null || version <= CurrentVersion) return null;

            if (!root.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0) return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                return new UpdateInfo
                {
                    Version = version.ToString(3),
                    Url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "",
                    Size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    Sha256 = ReadSha(root),
                };
            }
        }
        catch
        {
            // нет интернета или GitHub недоступен — молча пропускаем
        }
        return null;
    }

    /// <summary>Контрольная сумма лежит в описании релиза строкой «sha256: …».</summary>
    private static string ReadSha(JsonElement root)
    {
        if (!root.TryGetProperty("body", out var body)) return "";
        var text = body.GetString() ?? "";
        var index = text.IndexOf("sha256", StringComparison.OrdinalIgnoreCase);
        if (index < 0) return "";

        var rest = text[(index + 6)..].TrimStart(':', ' ', '*', '`');
        var hash = new string(rest.TakeWhile(char.IsLetterOrDigit).ToArray());
        return hash.Length == 64 ? hash.ToLowerInvariant() : "";
    }

    private static Version? ParseVersion(string tag)
    {
        tag = tag.TrimStart('v', 'V');
        return Version.TryParse(tag, out var version) ? version : null;
    }

    /// <summary>Скачать новый exe во временную папку с прогрессом 0–100.</summary>
    public static async Task<string?> DownloadAsync(UpdateInfo info, Action<int> progress, CancellationToken token)
    {
        try
        {
            var file = Path.Combine(Path.GetTempPath(), $"Kapsula-{info.Version}.exe");
            using var response = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? info.Size;
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using (var target = File.Create(file))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    done += read;
                    if (total > 0) progress((int)(done * 100 / total));
                }
            }

            if (info.Sha256.Length > 0)
            {
                var actual = await Sha256Async(file);
                if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    return null;
                }
            }

            return file;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> Sha256Async(string file)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Можно ли заменить exe на месте: пробуем записать файл рядом с ним.</summary>
    public static bool CanReplaceInPlace(string exePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir)) return false;

            var probe = Path.Combine(dir, ".kapsula-write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Скачать в «Загрузки» и открыть папку — когда на месте заменить нельзя.</summary>
    public static string MoveToDownloads(string file, string version)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var target = Path.Combine(downloads, $"Kapsula-{version}.exe");
        File.Move(file, target, true);
        return target;
    }

    /// <summary>Помощник: ждёт закрытия капсулы, подменяет exe и запускает новую версию.</summary>
    public static void ScheduleReplace(string newExe, string currentExe)
    {
        var script = Path.Combine(Path.GetTempPath(), "kapsula-update.cmd");
        var text = new StringBuilder()
            .AppendLine("@echo off")
            .AppendLine("chcp 65001 >nul")
            .AppendLine("timeout /t 2 /nobreak >nul")
            .AppendLine(":wait")
            .AppendLine($"tasklist /fi \"imagename eq {Path.GetFileName(currentExe)}\" | find /i \"{Path.GetFileName(currentExe)}\" >nul")
            .AppendLine("if not errorlevel 1 (")
            .AppendLine("  timeout /t 1 /nobreak >nul")
            .AppendLine("  goto wait")
            .AppendLine(")")
            .AppendLine($"copy /y \"{newExe}\" \"{currentExe}\" >nul")
            .AppendLine($"start \"\" \"{currentExe}\"")
            .AppendLine($"del \"{newExe}\" >nul 2>&1")
            .AppendLine("del \"%~f0\" >nul 2>&1")
            .ToString();

        File.WriteAllText(script, text, new UTF8Encoding(false));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
