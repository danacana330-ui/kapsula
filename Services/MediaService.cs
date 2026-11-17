using Windows.Media.Control;

namespace Kapsula.Services;

public sealed class MediaInfo
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string App { get; init; } = "";
    public bool IsPlaying { get; init; }
    public byte[]? Art { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    public bool HasSession => Title.Length > 0;
}

/// <summary>Текущий трек из системного медиа-хаба Windows (SMTC).</summary>
public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string _lastKey = "";

    public async Task InitAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _manager = null;
        }
    }

    private GlobalSystemMediaTransportControlsSession? _active;

    private GlobalSystemMediaTransportControlsSession? Session => _active ??= PickSession();

    /// <summary>
    /// «Текущая» сессия Windows — это последний активный плеер, и он может быть на паузе,
    /// пока музыка играет в другом приложении. Поэтому ищем именно играющую сессию.
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? PickSession()
    {
        var sessions = _manager?.GetSessions();
        if (sessions is null || sessions.Count == 0) return null;

        var current = _manager!.GetCurrentSession();
        if (current is not null && IsPlaying(current)) return current;

        foreach (var s in sessions)
            if (IsPlaying(s)) return s;

        return current ?? sessions[0];
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
    {
        try
        {
            return s.GetPlaybackInfo()?.PlaybackStatus
                   == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    public async Task<MediaInfo?> ReadAsync()
    {
        _active = null;
        var session = Session;
        if (session is null) return null;

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var info = session.GetPlaybackInfo();
            var title = props?.Title ?? "";
            var artist = props?.Artist ?? "";
            if (artist.Length == 0) artist = props?.AlbumArtist ?? "";
            var app = session.SourceAppUserModelId ?? "";

            byte[]? art = null;
            var key = app + "|" + title + "|" + artist;
            if (key != _lastKey)
            {
                _lastKey = key;
                art = await ReadThumbnailAsync(props);
            }

            var timeline = session.GetTimelineProperties();

            return new MediaInfo
            {
                Title = title,
                Artist = artist,
                App = FriendlyApp(app),
                IsPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                Art = art,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline is null ? TimeSpan.Zero : timeline.EndTime - timeline.StartTime,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? props)
    {
        try
        {
            var reference = props?.Thumbnail;
            if (reference is null) return null;
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0) return null;
            var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
            await stream.ReadAsync(buffer, (uint)stream.Size, Windows.Storage.Streams.InputStreamOptions.None);
            var bytes = new byte[buffer.Length];
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static string FriendlyApp(string aumid)
    {
        if (aumid.Contains("Spotify")) return "Spotify";
        if (aumid.Contains("Yandex")) return "Яндекс Музыка";
        if (aumid.Contains("Zvuk")) return "Звук";
        if (aumid.Contains("VK")) return "VK Музыка";
        if (aumid.Contains("Chrome")) return "Chrome";
        if (aumid.Contains("Edge")) return "Edge";
        if (aumid.Contains("Firefox")) return "Firefox";
        if (aumid.Contains("vlc", StringComparison.OrdinalIgnoreCase)) return "VLC";
        if (aumid.Contains("AIMP")) return "AIMP";
        if (aumid.Contains("foobar")) return "foobar2000";
        if (aumid.Contains("Telegram")) return "Telegram";
        return "";
    }

    public async Task PlayPauseAsync()
    {
        try { await Session?.TryTogglePlayPauseAsync(); } catch { }
    }

    public async Task NextAsync()
    {
        try { await Session?.TrySkipNextAsync(); } catch { }
    }

    public async Task PreviousAsync()
    {
        try { await Session?.TrySkipPreviousAsync(); } catch { }
    }
}
