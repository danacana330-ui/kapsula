using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kapsula.Services;
using Microsoft.Win32;

namespace Kapsula;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    private static readonly string[] Fonts =
    {
        "Segoe UI Variable Display",
        "Segoe UI",
        "Bahnschrift",
        "Arial",
        "Georgia",
        "Consolas",
        "Cascadia Mono",
        "Comic Sans MS",
    };

    public event Action? Saved;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        AutostartBox.IsChecked = settings.StartWithWindows;
        HoverBox.IsChecked = settings.ExpandOnHover;
        SecondsBox.IsChecked = settings.ShowSeconds;
        OsdBox.IsChecked = settings.ShowOsd;
        LightBox.IsChecked = settings.LightTheme;
        RimBox.IsChecked = settings.LightRim;
        ShadowBox.IsChecked = settings.ShowShadow;
        MotionBox.IsChecked = settings.ReduceMotion;
        CursorBox.IsChecked = settings.FollowCursor;
        PrivacyBox.IsChecked = settings.ShowPrivacyDots;
        FullscreenBox.IsChecked = settings.HideInFullscreen;
        ToastBox.IsChecked = settings.CopyToast;

        ScaleSlider.Value = Math.Clamp(settings.Scale * 100, 100, 199);
        ScaleValue.Text = $"{ScaleSlider.Value:0} %";
        ScaleSlider.ValueChanged += (_, _) => ScaleValue.Text = $"{ScaleSlider.Value:0} %";

        FontBox.ItemsSource = Fonts;
        FontBox.SelectedItem = Fonts.Contains(settings.UiFont) ? settings.UiFont : Fonts[0];
        try
        {
            FontFamily = new FontFamily(settings.UiFont);
        }
        catch
        {
        }

        CityBox.Text = settings.WeatherCity;
        AiBox.Text = settings.AiEndpoint;
        RecBox.Text = settings.RecordFolder;

        if (string.IsNullOrWhiteSpace(RecBox.Text))
            RecBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Kapsula");

        LightBox.Checked += (_, _) => Theme.SetLight(true);
        LightBox.Unchecked += (_, _) => Theme.SetLight(false);
        RimBox.Checked += (_, _) => Theme.SetRim(true);
        RimBox.Unchecked += (_, _) => Theme.SetRim(false);

        foreach (var def in Tiles.All)
        {
            var check = new CheckBox
            {
                Content = def.Title,
                Tag = def.Id,
                IsChecked = settings.HomeTiles.Contains(def.Id),
                Foreground = Theme.TextBrush,
                Margin = new Thickness(0, 0, 16, 8),
                MinWidth = 110,
            };
            TilesPanel.Children.Add(check);
        }

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        };
    }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Куда сохранять записи" };
        if (dialog.ShowDialog() == true) RecBox.Text = dialog.FolderName;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = AutostartBox.IsChecked == true;
        _settings.ExpandOnHover = HoverBox.IsChecked == true;
        _settings.ShowSeconds = SecondsBox.IsChecked == true;
        _settings.ShowOsd = OsdBox.IsChecked == true;
        _settings.LightTheme = LightBox.IsChecked == true;
        _settings.LightRim = RimBox.IsChecked == true;
        _settings.ShowShadow = ShadowBox.IsChecked == true;
        _settings.ReduceMotion = MotionBox.IsChecked == true;
        _settings.FollowCursor = CursorBox.IsChecked == true;
        _settings.ShowPrivacyDots = PrivacyBox.IsChecked == true;
        _settings.HideInFullscreen = FullscreenBox.IsChecked == true;
        _settings.CopyToast = ToastBox.IsChecked == true;
        _settings.Scale = Math.Round(ScaleSlider.Value / 100, 2);
        _settings.UiFont = FontBox.SelectedItem as string ?? Fonts[0];
        _settings.WeatherCity = CityBox.Text.Trim();
        _settings.AiEndpoint = AiBox.Text.Trim();
        _settings.RecordFolder = RecBox.Text.Trim();
        _settings.HomeTiles = TilesPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Tag)
            .ToList();
        _settings.Save();

        Autostart.Apply(_settings.StartWithWindows);
        Saved?.Invoke();
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        // откатываем предпросмотр темы, если настройки не сохранили
        Theme.SetLight(_settings.LightTheme);
        Theme.SetRim(_settings.LightRim);
        Close();
    }
}
