using System.IO;
using System.Windows.Media;

namespace Kapsula.Services;

/// <summary>Свой простой плеер: капсула сама включает музыку, не дожидаясь, пока откроют другое приложение.</summary>
public static class MusicPlayer
{
    private static readonly MediaPlayer Player = new();
    private static List<string> _tracks = new();
    private static int _index = -1;
    private static bool _playing;

    public static event Action? Changed;

    public static string Folder =>
        string.IsNullOrWhiteSpace(App.Settings.MusicFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            : App.Settings.MusicFolder;

    public static IReadOnlyList<string> Tracks
    {
        get
        {
            if (_tracks.Count == 0) Scan();
            return _tracks;
        }
    }

    public static string CurrentTitle =>
        _index >= 0 && _index < _tracks.Count ? Path.GetFileNameWithoutExtension(_tracks[_index]) : "";

    public static bool IsPlaying => _playing;
    public static bool HasTrack => _index >= 0;

    public static TimeSpan Position => Player.Position;
    public static TimeSpan Length => Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero;

    public static void Scan()
    {
        _tracks = new List<string>();
        try
        {
            if (!Directory.Exists(Folder)) return;
            var allowed = new[] { ".mp3", ".wav", ".wma", ".m4a", ".aac" };
            _tracks = Directory.EnumerateFiles(Folder, "*.*", SearchOption.AllDirectories)
                .Where(f => allowed.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
        }
    }

    public static void Toggle()
    {
        if (!HasTrack)
        {
            PlayAt(0);
            return;
        }

        if (_playing)
        {
            Player.Pause();
            _playing = false;
        }
        else
        {
            Player.Play();
            _playing = true;
        }
        Changed?.Invoke();
    }

    public static void PlayAt(int index)
    {
        var tracks = Tracks;
        if (tracks.Count == 0) return;
        _index = ((index % tracks.Count) + tracks.Count) % tracks.Count;
        Player.Open(new Uri(tracks[_index]));
        Player.Play();
        _playing = true;
        Changed?.Invoke();
    }

    public static void Next() => PlayAt(_index + 1);
    public static void Previous() => PlayAt(_index - 1);

    public static void Stop()
    {
        Player.Stop();
        _playing = false;
        Changed?.Invoke();
    }

    public static void SetVolume(double volume) => Player.Volume = Math.Clamp(volume, 0, 1);
}
