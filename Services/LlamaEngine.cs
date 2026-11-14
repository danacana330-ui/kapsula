using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Kapsula.Services;

/// <summary>Движок llama.cpp: скачивает сборку один раз и запускает llama-server на своём порту.</summary>
public static class LlamaEngine
{
    private const string ReleasesApi = "https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=10";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kapsula", "engine");

    public static string ServerPath => Path.Combine(Dir, "llama-server.exe");

    public static bool IsInstalled => File.Exists(ServerPath);

    /// <summary>Скачать CPU-сборку llama.cpp и распаковать рядом.</summary>
    public static async Task<bool> EnsureAsync(Action<int> progress, CancellationToken token)
    {
        if (IsInstalled) return true;

        try
        {
            Directory.CreateDirectory(Dir);
            var url = await FindCpuBuildAsync();
            if (url is null) return false;

            var zip = Path.Combine(Path.GetTempPath(), "llama-cpu.zip");
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                await using var source = await response.Content.ReadAsStreamAsync(token);
                await using var target = File.Create(zip);
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

            ZipFile.ExtractToDirectory(zip, Dir, true);
            File.Delete(zip);

            // в архиве файлы лежат в подпапке — поднимаем их на уровень выше
            Flatten();
            return IsInstalled;
        }
        catch
        {
            return false;
        }
    }

    private static void Flatten()
    {
        try
        {
            var nested = Directory.EnumerateDirectories(Dir).FirstOrDefault();
            if (nested is null || File.Exists(ServerPath)) return;

            foreach (var file in Directory.EnumerateFiles(nested, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Dir, Path.GetFileName(file));
                if (!File.Exists(target)) File.Move(file, target);
            }
        }
        catch
        {
        }
    }

    /// <summary>Ищем в последних релизах сборку вида llama-bNNNNN-bin-win-cpu-x64.zip.</summary>
    private static async Task<string?> FindCpuBuildAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
            request.Headers.UserAgent.ParseAdd("Kapsula");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("assets", out var assets)) continue;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.Contains("bin-win-cpu-x64.zip", StringComparison.OrdinalIgnoreCase)) continue;
                    return asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                }
            }
        }
        catch
        {
        }
        return null;
    }
}

/// <summary>Запущенный llama-server: держит модель в памяти и отвечает по OpenAI-совместимому API.</summary>
public sealed class LlamaServer : IDisposable
{
    private Process? _process;

    public int Port { get; private set; }

    public bool IsRunning => _process is { HasExited: false };

    public string? ModelPath { get; private set; }

    public async Task<bool> StartAsync(string modelPath, CancellationToken token)
    {
        if (IsRunning && ModelPath == modelPath) return true;
        Stop();

        if (!File.Exists(LlamaEngine.ServerPath) || !File.Exists(modelPath)) return false;

        Port = FreePort();
        var info = new ProcessStartInfo(LlamaEngine.ServerPath)
        {
            WorkingDirectory = LlamaEngine.Dir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = $"-m \"{modelPath}\" -c 4096 --port {Port} --host 127.0.0.1",
        };

        try
        {
            _process = Process.Start(info);
        }
        catch
        {
            return false;
        }
        if (_process is null) return false;

        // потоки обязательно вычитываем, иначе сервер встанет на переполненном буфере
        _ = _process.StandardOutput.ReadToEndAsync();
        _ = _process.StandardError.ReadToEndAsync();

        ModelPath = modelPath;
        return await WaitReadyAsync(token);
    }

    private async Task<bool> WaitReadyAsync(CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            if (token.IsCancellationRequested) return false;
            if (_process is null || _process.HasExited) return false;

            try
            {
                using var response = await client.GetAsync($"http://127.0.0.1:{Port}/health", token);
                if (response.IsSuccessStatusCode) return true;
            }
            catch
            {
                // сервер ещё поднимает модель
            }
            await Task.Delay(700, token);
        }
        return false;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false }) _process.Kill(true);
        }
        catch
        {
        }
        try
        {
            _process?.Dispose();
        }
        catch
        {
        }
        _process = null;
        ModelPath = null;
    }

    public void Dispose() => Stop();
}

/// <summary>Один llama-server на всё приложение: панель можно закрыть, модель остаётся в памяти.</summary>
public static class MiniRuntime
{
    public static LlamaServer Server { get; } = new();
}

/// <summary>Чат с мини-моделью через llama-server (OpenAI-совместимый поток).</summary>
public sealed class MiniAiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private const string SystemPrompt =
        "Ты помощник внутри небольшого приложения Kapsula для Windows. " +
        "Всегда отвечай по-русски, кратко и по делу: одна-три фразы, без вступлений и повторов вопроса.";

    private readonly LlamaServer _server;

    public MiniAiService(LlamaServer server) => _server = server;

    public async Task<string> ChatAsync(IReadOnlyList<AiMessage> messages, Action<string> onToken, CancellationToken token)
    {
        var payload = JsonSerializer.Serialize(new
        {
            stream = true,
            messages = new[] { new { role = "system", content = SystemPrompt } }
                .Concat(messages.Select(m => new { role = m.Role, content = m.Content }))
                .ToArray(),
        });

        var full = new StringBuilder();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"http://127.0.0.1:{_server.Port}/v1/chat/completions")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                token.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                var json = line[5..].Trim();
                if (json == "[DONE]") break;

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                        continue;
                    if (!choices[0].TryGetProperty("delta", out var delta)) continue;
                    if (!delta.TryGetProperty("content", out var content)) continue;

                    var piece = content.GetString() ?? "";
                    if (piece.Length == 0) continue;
                    full.Append(piece);
                    onToken(piece);
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
            full.Append("\n[остановлено]");
        }
        catch (Exception ex)
        {
            full.Append("\n[не удалось получить ответ: " + ex.Message + "]");
        }
        return full.ToString();
    }
}
