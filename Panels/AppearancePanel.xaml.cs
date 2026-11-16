using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class AppearancePanel : UserControl
{
    public event Action? Back;

    private readonly AppSettings _s = App.Settings;

    public AppearancePanel()
    {
        InitializeComponent();

        foreach (var def in Tiles.All)
        {
            var check = new CheckBox
            {
                Content = def.Title,
                Tag = def.Id,
                IsChecked = _s.HomeTiles.Contains(def.Id),
                Foreground = Theme.TextBrush,
                Margin = new Thickness(0, 0, 14, 8),
                MinWidth = 96,
            };
            check.Checked += OnTiles;
            check.Unchecked += OnTiles;
            TilesPanel.Children.Add(check);
        }

        CleanupBox.Text = _s.ClipboardCleanupHours.ToString();
        CleanupBox.TextChanged += (_, _) => Save();

        SummaryBox.IsChecked = _s.SummaryEnabled;
        SummaryBox.Checked += (_, _) => Save();
        SummaryBox.Unchecked += (_, _) => Save();

        SummaryTimeBox.Text = _s.SummaryTime;
        SummaryTimeBox.TextChanged += (_, _) => Save();

        LightBox.IsChecked = _s.LightTheme;
        LightBox.Checked += (_, _) => Save();
        LightBox.Unchecked += (_, _) => Save();

        CursorBox.IsChecked = _s.FollowCursor;
        CursorBox.Checked += (_, _) => Save();
        CursorBox.Unchecked += (_, _) => Save();

        MotionBox.IsChecked = _s.ReduceMotion;
        MotionBox.Checked += (_, _) => Save();
        MotionBox.Unchecked += (_, _) => Save();

        FullscreenBox.IsChecked = _s.HideInFullscreen;
        FullscreenBox.Checked += (_, _) => Save();
        FullscreenBox.Unchecked += (_, _) => Save();
    }

    private void OnTiles(object sender, RoutedEventArgs e)
    {
        _s.HomeTiles = TilesPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Tag)
            .ToList();
        Save();
    }

    private void Save()
    {
        if (int.TryParse(CleanupBox.Text.Trim(), out var hours))
            _s.ClipboardCleanupHours = Math.Clamp(hours, 0, 8760);

        _s.SummaryEnabled = SummaryBox.IsChecked == true;
        var time = SummaryTimeBox.Text.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(time, @"^\d{1,2}:\d{2}$")) _s.SummaryTime = time;
        _s.LightTheme = LightBox.IsChecked == true;
        _s.FollowCursor = CursorBox.IsChecked == true;
        _s.ReduceMotion = MotionBox.IsChecked == true;
        _s.HideInFullscreen = FullscreenBox.IsChecked == true;

        _s.Save();
        MainWindow.Instance?.ApplySettings();
        ClipboardStore.Cleanup();
    }

    private void OnAllSettings(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_s);
        window.Saved += () => MainWindow.Instance?.ApplySettings();
        window.Show();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
