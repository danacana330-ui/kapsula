using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kapsula.Services;
using Microsoft.Win32;

namespace Kapsula.Panels;

/// <summary>Все настройки капсулы внутри неё: разделы слева, карточки справа.</summary>
public partial class AppearancePanel : UserControl
{
    public event Action? Back;

    private readonly AppSettings _s = App.Settings;
    private bool _loading;

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

    private static readonly (string Id, string Title, string Glyph)[] Sections =
    {
        ("capsule", "Капсула", "\uE7F4"),
        ("look", "Внешний вид", "\uE790"),
        ("summary", "Сводка", "\uE946"),
        ("ai", "ИИ", "\uE99A"),
        ("data", "Данные", "\uE8F1"),
    };

    public AppearancePanel()
    {
        InitializeComponent();

        BuildSections();
        BuildTiles();
        WireEvents();
        Select("capsule");
        LoadValues();
    }

    // ── разделы ──────────────────────────────────────────────────────────────

    private void BuildSections()
    {
        SectionBar.Children.Clear();
        foreach (var (id, title, glyph) in Sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0),
            });
            row.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var button = new Button
            {
                Content = row,
                Tag = id,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(10, 7, 10, 7),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = (Style)FindResource("SectionButton"),
            };
            button.Click += (_, _) => Select(id);
            SectionBar.Children.Add(button);
        }
    }

    private void Select(string id)
    {
        foreach (var button in SectionBar.Children.OfType<Button>())
        {
            var active = (string)button.Tag == id;
            button.Background = active ? Theme.CardHoverBrush : Brushes.Transparent;
            if (button.Content is StackPanel row)
            {
                foreach (var text in row.Children.OfType<TextBlock>())
                {
                    text.Foreground = active ? Theme.AccentBrush : Theme.DimBrush;
                    text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        SecCapsule.Visibility = id == "capsule" ? Visibility.Visible : Visibility.Collapsed;
        SecLook.Visibility = id == "look" ? Visibility.Visible : Visibility.Collapsed;
        SecSummary.Visibility = id == "summary" ? Visibility.Visible : Visibility.Collapsed;
        SecAi.Visibility = id == "ai" ? Visibility.Visible : Visibility.Collapsed;
        SecData.Visibility = id == "data" ? Visibility.Visible : Visibility.Collapsed;

        Crumb.Text = Sections.First(s => s.Id == id).Title.ToLowerInvariant();
    }

    // ── значения ─────────────────────────────────────────────────────────────

    private void BuildTiles()
    {
        TilesPanel.Children.Clear();
        foreach (var def in Tiles.All)
        {
            var check = new CheckBox
            {
                Content = def.Title,
                Tag = def.Id,
                Style = (Style)FindResource("SwitchChip"),
            };
            check.Checked += OnTiles;
            check.Unchecked += OnTiles;
            TilesPanel.Children.Add(check);
        }
    }

    private void WireEvents()
    {
        foreach (var box in new[]
                 {
                     AutostartBox, HoverBox, KeepBox, SecondsBox, OsdBox, FullscreenBox, CursorBox, MotionBox,
                     LightBox, RimBox, ShadowBox, PrivacyBox, SummaryBox,
                     SkipPwdBox, ToastBox, RecAudioBox,
                 })
        {
            box.Checked += (_, _) => Save();
            box.Unchecked += (_, _) => Save();
        }

        ScaleSlider.ValueChanged += (_, _) =>
        {
            ScaleValue.Text = $"{ScaleSlider.Value:0} %";
            Save();
        };

        FontBox.ItemsSource = Fonts;
        FontBox.SelectionChanged += (_, _) => Save();

        CleanupBox.TextChanged += (_, _) => Save();
        CityBox.LostFocus += (_, _) => Save();
        AiBox.LostFocus += (_, _) => Save();
        AiKeyBox.LostFocus += (_, _) => Save();
        AiModelBox.LostFocus += (_, _) => Save();
        RecBox.LostFocus += (_, _) => Save();
    }

    private void LoadValues()
    {
        _loading = true;

        AutostartBox.IsChecked = _s.StartWithWindows;
        HoverBox.IsChecked = _s.ExpandOnHover;
        KeepBox.IsChecked = _s.KeepOpen;
        SecondsBox.IsChecked = _s.ShowSeconds;
        OsdBox.IsChecked = _s.ShowOsd;
        FullscreenBox.IsChecked = _s.HideInFullscreen;
        CursorBox.IsChecked = _s.FollowCursor;
        MotionBox.IsChecked = _s.ReduceMotion;

        LightBox.IsChecked = _s.LightTheme;
        RimBox.IsChecked = _s.LightRim;
        ShadowBox.IsChecked = _s.ShowShadow;
        PrivacyBox.IsChecked = _s.ShowPrivacyDots;

        SummaryBox.IsChecked = _s.SummaryEnabled;

        SkipPwdBox.IsChecked = _s.SkipPasswords;
        ToastBox.IsChecked = _s.CopyToast;
        RecAudioBox.IsChecked = _s.RecordSystemAudio;

        ScaleSlider.Value = Math.Clamp(_s.Scale * 100, 100, 199);
        ScaleValue.Text = $"{ScaleSlider.Value:0} %";

        FontBox.SelectedItem = Fonts.Contains(_s.UiFont) ? _s.UiFont : Fonts[0];

        CleanupBox.Text = _s.ClipboardCleanupHours.ToString();
        SummaryTimeText.Text = _s.SummaryTime;
        CityBox.Text = _s.WeatherCity;
        AiBox.Text = _s.AiEndpoint;
        AiKeyBox.Text = _s.AiServerKey;
        AiModelBox.Text = _s.AiServerModel;
        RecBox.Text = string.IsNullOrWhiteSpace(_s.RecordFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Kapsula")
            : _s.RecordFolder;

        foreach (var check in TilesPanel.Children.OfType<CheckBox>())
            check.IsChecked = _s.HomeTiles.Contains((string)check.Tag);

        ShortcutButton.Content = "Создать ярлык на рабочем столе";
        _loading = false;
    }

    private void OnTiles(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _s.HomeTiles = TilesPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Tag)
            .ToList();
        Save();
    }

    private void Save()
    {
        if (_loading) return;

        _s.StartWithWindows = AutostartBox.IsChecked == true;
        _s.ExpandOnHover = HoverBox.IsChecked == true;
        _s.KeepOpen = KeepBox.IsChecked == true;
        _s.ShowSeconds = SecondsBox.IsChecked == true;
        _s.ShowOsd = OsdBox.IsChecked == true;
        _s.HideInFullscreen = FullscreenBox.IsChecked == true;
        _s.FollowCursor = CursorBox.IsChecked == true;
        _s.ReduceMotion = MotionBox.IsChecked == true;

        _s.LightTheme = LightBox.IsChecked == true;
        _s.LightRim = RimBox.IsChecked == true;
        _s.ShowShadow = ShadowBox.IsChecked == true;
        _s.ShowPrivacyDots = PrivacyBox.IsChecked == true;

        _s.SummaryEnabled = SummaryBox.IsChecked == true;

        _s.SkipPasswords = SkipPwdBox.IsChecked == true;
        _s.CopyToast = ToastBox.IsChecked == true;
        _s.RecordSystemAudio = RecAudioBox.IsChecked == true;

        _s.Scale = Math.Round(ScaleSlider.Value / 100, 2);
        _s.UiFont = FontBox.SelectedItem as string ?? Fonts[0];

        if (int.TryParse(CleanupBox.Text.Trim(), out var hours))
            _s.ClipboardCleanupHours = Math.Clamp(hours, 0, 8760);

        if (System.Text.RegularExpressions.Regex.IsMatch(SummaryTimeText.Text, @"^\d{1,2}:\d{2}$"))
            _s.SummaryTime = SummaryTimeText.Text;

        if (CityBox.Text.Trim().Length > 0) _s.WeatherCity = CityBox.Text.Trim();
        _s.AiEndpoint = AiBox.Text.Trim();
        _s.AiServerKey = AiKeyBox.Text.Trim();
        _s.AiServerModel = AiModelBox.Text.Trim();
        _s.RecordFolder = RecBox.Text.Trim();

        _s.Save();
        Autostart.Apply(_s.StartWithWindows);
        MainWindow.Instance?.ApplySettings();
        ClipboardStore.Cleanup();
    }

    // ── кнопки ───────────────────────────────────────────────────────────────

    private void OnSummaryEarlier(object sender, RoutedEventArgs e) => ShiftSummaryTime(-30);

    private void OnSummaryLater(object sender, RoutedEventArgs e) => ShiftSummaryTime(30);

    /// <summary>Время сводки двигаем стрелками по полчаса — писать руками ничего не нужно.</summary>
    private void ShiftSummaryTime(int minutes)
    {
        var text = SummaryTimeText.Text.Trim().Replace('.', ':');
        if (!TimeSpan.TryParse(text, out var at)) at = new TimeSpan(9, 0, 0);
        var total = ((int)at.TotalMinutes + minutes) % 1440;
        if (total < 0) total += 1440;
        at = TimeSpan.FromMinutes(total);
        SummaryTimeText.Text = $"{at.Hours:00}:{at.Minutes:00}";
        _s.SummaryTime = SummaryTimeText.Text;
        _s.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "Вернуть все настройки как при первом запуске? Заметки, буфер и файлы останутся на месте.",
            "Kapsula", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var fresh = new AppSettings();
        foreach (var property in typeof(AppSettings).GetProperties().Where(p => p.CanWrite))
            property.SetValue(_s, property.GetValue(fresh));

        _s.Save();
        LoadValues();
        Autostart.Apply(_s.StartWithWindows);
        MainWindow.Instance?.ApplySettings();
    }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Куда сохранять записи" };
        if (dialog.ShowDialog() == true)
        {
            RecBox.Text = dialog.FolderName;
            Save();
        }
    }

    private void OnMakeShortcut(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            var link = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kapsula.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(link);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
            shortcut.Description = "Kapsula — динамический остров";
            shortcut.Save();

            ShortcutButton.Content = "Ярлык создан";
        }
        catch (Exception ex)
        {
            ShortcutButton.Content = "Не вышло: " + ex.Message;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
