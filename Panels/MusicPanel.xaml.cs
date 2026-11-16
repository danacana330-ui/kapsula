using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class MusicPanel : UserControl
{
    public event Action? Back;

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };

    public MusicPanel()
    {
        InitializeComponent();
        FolderText.Text = MusicPlayer.Folder;
        Fill();
        _tick.Tick += (_, _) => Refresh();
        _tick.Start();
        Unloaded += (_, _) => _tick.Stop();
    }

    private void Fill()
    {
        List.Items.Clear();
        foreach (var track in MusicPlayer.Tracks)
        {
            List.Items.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = Path.GetFileNameWithoutExtension(track),
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = Theme.TextBrush,
                },
                Tag = track,
            });
        }

        if (List.Items.Count == 0)
            TitleText.Text = "в папке нет музыки";
    }

    private void Refresh()
    {
        TitleText.Text = MusicPlayer.HasTrack ? MusicPlayer.CurrentTitle : "ничего не играет";
        PlayGlyph.Text = MusicPlayer.IsPlaying ? "\uE769" : "\uE768";
        PlaceText.Text = MusicPlayer.HasTrack
            ? $"{MusicPlayer.Position:mm\\:ss} / {MusicPlayer.Length:mm\\:ss}"
            : $"найдено треков: {MusicPlayer.Tracks.Count}";

        Bar.Value = MusicPlayer.Length.TotalSeconds > 0
            ? MusicPlayer.Position.TotalSeconds / MusicPlayer.Length.TotalSeconds
            : 0;
    }

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        MusicPlayer.Toggle();
        Refresh();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        MusicPlayer.Next();
        Refresh();
    }

    private void OnPrevious(object sender, RoutedEventArgs e)
    {
        MusicPlayer.Previous();
        Refresh();
    }

    private void OnPick(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not ListBoxItem { Tag: string path }) return;
        var index = MusicPlayer.Tracks.ToList().IndexOf(path);
        List.SelectedItem = null;
        if (index >= 0) MusicPlayer.PlayAt(index);
        Refresh();
    }

    private void OnRescan(object sender, RoutedEventArgs e)
    {
        MusicPlayer.Scan();
        Fill();
        Refresh();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        Back?.Invoke();
    }
}
