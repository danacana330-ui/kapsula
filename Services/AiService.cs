using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Kapsula.Services;

public sealed class AiMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public sealed class OllamaModel
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SizeText => SizeBytes >= 1_000_000_000
        ? $"{SizeBytes / 1_000_000_000.0:0.0} ГБ"
        : $"{SizeBytes / 1_000_000.0:0} МБ";
}

/// <summary>Одна роль помощника для всех источников модели — мини, Ollama, свой сервер, облако.</summary>
public static class AiPrompt
{
    public const string System =
        "Ты помощник внутри небольшого приложения Kapsula для Windows. " +
        "Всегда отвечай по-русски, кратко и по делу: одна-три фразы, без вступлений и повторов вопроса.";
}

public static class AiHistory
{
    /// <summary>
    /// Оставляет свежие сообщения в пределах лимита символов: у мини-модели контекст 4096 токенов,
    /// и без обрезки длинный чат молча теряет начало.
    /// </summary>
    public static List<AiMessage> Trim(IReadOnlyList<AiMessage> messages, int maxChars)
    {
        var kept = new List<AiMessage>();
        var total = 0;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var length = messages[i].Content?.Length ?? 0;
            if (total + length > maxChars && kept.Count > 0) break;
            total += length;
            kept.Insert(0, messages[i]);
        }
        return kept;
    }
}

/// <summary>Свой сервер: любой OpenAI-совместимый адрес с ключом и именем модели.</summary>
public static class CustomServer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static string Normalize(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (url.Length == 0) return "";
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        if (!url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) url += "/v1";
        return url;
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string key, string? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(key))
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key.Trim());
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    /// <summary>Список моделей сервера. Ошибку возвращаем текстом, чтобы её показать человеку.</summary>
    public static async Task<(List<string> Models, string? Error)> ListAsync(string url, string key, CancellationToken token)
    {
        var models = new List<string>();
        var normalized = Normalize(url);
        if (normalized.Length == 0) return (models, "адрес не заполнен");
        try
        {
            using var response = await Http.SendAsync(Request(HttpMethod.Get, normalized + "/models", key, null), token);
            if (!response.IsSuccessStatusCode)
                return (models, $"сервер ответил {(int)response.StatusCode}");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (doc.RootElement.TryGetProperty("data", out var data))
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } name)
                        models.Add(name);
            return (models, null);
        }
        catch (Exception ex)
        {
            return (models, ex.Message);
        }
    }

    public static async Task<string> ChatAsync(string url, string key, string model,
        IReadOnlyList<AiMessage> messages, Action<string> onToken, CancellationToken token)
    {
        var normalized = Normalize(url);
        var trimmed = AiHistory.Trim(messages, 24000);
        var payload = JsonSerializer.Serialize(new
        {
            model,
            stream = true,
            messages = new[] { new { role = "system", content = AiPrompt.System } }
                .Concat(trimmed.Select(m => new { role = m.Role, content = m.Content })).ToArray(),
        });

        var full = new StringBuilder();
        try
        {
            using var response = await Http.SendAsync(
                Request(HttpMethod.Post, normalized + "/chat/completions", key, payload),
                HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(token);
                if (text.Length > 300) text = text[..300];
                return full.Append($"не удалось получить ответ: сервер ответил {(int)response.StatusCode}. {text}").ToString();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                token.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
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
            full.Append("\n[сервер не ответил: " + ex.Message + "]");
        }
        return full.ToString();
    }
}

/// <summary>Бесплатные облачные модели через локальный шлюз: быстро, но нужен интернет.</summary>
public static class FreeCloud
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <summary>Шлюз бесплатных облачных моделей (Desktop\FreeLLM). null — шлюз не поднят.</summary>
    public static async Task<List<OllamaModel>> ListAsync(int port)
    {
        var list = new List<OllamaModel>();
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"http://127.0.0.1:{port}/api/tags"));
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                var name = m.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("free-", StringComparison.Ordinal)) continue;
                list.Add(new OllamaModel { Name = name });
            }
        }
        catch
        {
        }
        return list;
    }

    /// <summary>Потоковый чат через шлюз — тот же SSE, что отдаёт llama-server для мини-моделей.</summary>
    public static async Task<string> ChatAsync(int port, string model, IReadOnlyList<AiMessage> messages,
        Action<string> onToken, CancellationToken token)
    {
        var trimmed = AiHistory.Trim(messages, 24000);
        var payload = JsonSerializer.Serialize(new
        {
            model,
            stream = true,
            messages = new[] { new { role = "system", content = AiPrompt.System } }
                .Concat(trimmed.Select(m => new { role = m.Role, content = m.Content })).ToArray(),
        });

        var full = new StringBuilder();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"http://127.0.0.1:{port}/v1/chat/completions")
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
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
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
            full.Append("\n[бесплатная модель не ответила: " + ex.Message + "]");
        }
        return full.ToString();
    }
}

public sealed class AiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly string _base;

    public AiService(string baseUrl) => _base = baseUrl.TrimEnd('/');

    public async Task<bool> IsRunningAsync()
    {
        try
        {
            using var r = await Http.GetAsync(_base + "/api/tags");
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModel>> ListAsync()
    {
        var list = new List<OllamaModel>();
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(_base + "/api/tags"));
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                list.Add(new OllamaModel
                {
                    Name = m.GetProperty("name").GetString() ?? "",
                    SizeBytes = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                });
            }
        }
        catch
        {
        }
        return list.OrderBy(m => m.Name).ToList();
    }

    public async Task<string> ChatAsync(string model, IReadOnlyList<AiMessage> messages, Action<string> onToken, CancellationToken token)
    {
        var trimmed = AiHistory.Trim(messages, 12000);
        var payload = JsonSerializer.Serialize(new
        {
            model,
            stream = true,
            messages = new[] { new { role = "system", content = AiPrompt.System } }
                .Concat(trimmed.Select(m => new { role = m.Role, content = m.Content })).ToArray(),
        });

        var request = new HttpRequestMessage(HttpMethod.Post, _base + "/api/chat")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        var full = new StringBuilder();
        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                token.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("content", out var content))
                    {
                        var piece = content.GetString() ?? "";
                        full.Append(piece);
                        onToken(piece);
                    }
                    if (doc.RootElement.TryGetProperty("error", out var err))
                        return full.Append("\n[ошибка Ollama: " + err.GetString() + "]").ToString();
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
            full.Append("\n[не удалось связаться с Ollama: " + ex.Message + "]");
        }
        return full.ToString();
    }

    public async Task PullAsync(string model, Action<string> onStatus, CancellationToken token)
    {
        var payload = JsonSerializer.Serialize(new { name = model, stream = true });
        var request = new HttpRequestMessage(HttpMethod.Post, _base + "/api/pull")
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
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status))
                {
                    var text = status.GetString() ?? "";
                    if (root.TryGetProperty("total", out var total) && total.GetInt64() > 0)
                    {
                        var done = root.TryGetProperty("completed", out var c) ? c.GetInt64() : 0;
                        text += $" {done * 100 / total.GetInt64()}%";
                    }
                    onStatus(text);
                }
            }
            catch (JsonException)
            {
            }
        }
    }

    /// <summary>Сколько ОЗУ на машине — по нему советуем модель.</summary>
    public static double RamGb()
    {
        var info = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref info)) return 8;
        return info.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
    }

    public static (string Model, string Why) Recommend()
    {
        var ram = RamGb();
        return ram switch
        {
            < 8 => ("qwen2.5:1.5b", "самая лёгкая, пойдёт на слабом железе"),
            < 16 => ("qwen2.5:3b", "небольшая, отвечает быстро"),
            < 32 => ("qwen2.5:7b", "хороший баланс качества и скорости"),
            _ => ("qwen2.5:14b", "железо позволяет модель побольше"),
        };
    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
