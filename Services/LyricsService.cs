using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kapsula.Services;

public sealed class Lyrics
{
    public string Plain { get; init; } = "";
    public List<(double At, string Text)> Lines { get; init; } = new();

    public bool HasLines => Lines.Count > 0;

    /// <summary>Строка, которая звучит на этой секунде.</summary>
    public string LineAt(double seconds)
    {
        if (Lines.Count == 0) return "";
        var current = "";
        foreach (var (at, text) in Lines)
        {
            if (at > seconds + 0.3) break;
            current = text;
        }

        return current;
    }
}

/// <summary>Текст песни из LRCLIB: синхронные строки LRC и обычный текст, с кэшем на диске.</summary>
public static class LyricsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kapsula", "lyrics");

    public static async Task<Lyrics?> GetAsync(string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var key = (artist + "|" + title).Trim().ToLowerInvariant();
        var cached = ReadCache(key);
        if (cached is not null) return cached;

        var lyrics = await FetchAsync(artist, title);
        if (lyrics is not null) WriteCache(key, lyrics);
        return lyrics;
    }

    private static async Task<Lyrics?> FetchAsync(string artist, string title)
    {
        var json = await AskAsync("https://lrclib.net/api/get?artist_name=" +
                                  Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(title));
        var lyrics = Parse(json);
        if (lyrics is not null) return lyrics;

        json = await AskAsync("https://lrclib.net/api/search?q=" +
                              Uri.EscapeDataString((artist + " " + title).Trim()));
        return ParseFirst(json);
    }

    private static async Task<string?> AskAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Kapsula/2.0 (https://github.com/danacana330-ui/kapsula)");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    private static Lyrics? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return FromElement(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static Lyrics? ParseFirst(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var lyrics = FromElement(item);
                if (lyrics is not null) return lyrics;
            }
        }
        catch
        {
        }

        return null;
    }

    private static Lyrics? FromElement(JsonElement item)
    {
        var plain = item.TryGetProperty("plainLyrics", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? ""
            : "";
        var synced = item.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? ""
            : "";

        var lines = ParseLrc(synced);
        if (lines.Count == 0 && plain.Length == 0) return null;

        return new Lyrics
        {
            Plain = plain.Length > 0 ? plain : string.Join("\n", lines.Select(l => l.Text)),
            Lines = lines,
        };
    }

    /// <summary>Разбор LRC: `[00:35.66] строка`, метки времени может быть несколько в одной строке.</summary>
    private static List<(double At, string Text)> ParseLrc(string lrc)
    {
        var result = new List<(double, string)>();
        if (string.IsNullOrWhiteSpace(lrc)) return result;

        foreach (var raw in lrc.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var times = new List<double>();
            while (line.StartsWith('['))
            {
                var close = line.IndexOf(']');
                if (close < 0) break;
                var stamp = line[1..close];
                line = line[(close + 1)..].TrimStart();

                var parts = stamp.Split(':', '.');
                if (parts.Length < 2) break;
                if (!double.TryParse(parts[0], out var minutes)) break;
                if (!double.TryParse(parts[1], out var seconds)) break;
                var fraction = parts.Length > 2 && double.TryParse(parts[2], out var f) ? f / 100 : 0;
                times.Add(minutes * 60 + seconds + fraction);
            }

            if (times.Count == 0 || line.Length == 0) continue;
            foreach (var at in times) result.Add((at, line));
        }

        result.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return result;
    }

    private static Lyrics? ReadCache(string key)
    {
        try
        {
            var path = Path.Combine(Dir, Hash(key) + ".json");
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            return FromElement(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteCache(string key, Lyrics lyrics)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var data = new
            {
                plainLyrics = lyrics.Plain,
                syncedLyrics = string.Join("\n", lyrics.Lines.Select(l =>
                    "[" + TimeSpan.FromSeconds(l.At).ToString(@"mm\:ss\.ff") + "] " + l.Text)),
            };
            File.WriteAllText(Path.Combine(Dir, Hash(key) + ".json"), JsonSerializer.Serialize(data));
        }
        catch
        {
        }
    }

    private static string Hash(string text)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
