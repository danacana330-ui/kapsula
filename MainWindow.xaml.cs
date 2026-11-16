using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kapsula.Panels;
using Kapsula.Screenshot;
using Kapsula.Services;

namespace Kapsula;

public partial class MainWindow : Window
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_LAUNCHER = 1;
    private const int HOTKEY_SHOT = 2;
    private const int HOTKEY_RECORD = 3;
    private const int HOTKEY_SHOT_ALT = 4;
    private const int HOTKEY_RECORD_ALT = 5;
    private const int HOTKEY_SEARCH = 6;
    private const int HOTKEY_NOTE = 7;
    private const int HOTKEY_TRANSLATE = 8;

    private readonly MediaService _media = new();
    private readonly RecordingService _recorder = new();
    private AppSettings _settings = App.Settings;
    private OsdWindow? _osd;
    private SummaryStripWindow? _updateStrip;
    private UpdateInfo? _update;
    private double _lastVolume = -1;
    private bool _lastCaps;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };

    private bool _expanded;
    private bool _animating;
    private bool _userPinned;
    private bool _volumeDragging;
    private bool _hasMedia;
    private DateTime _lastMediaOk;
    private TextBlock? _recordLabel;
    private string _lastClip = "";
    private MediaInfo? _current;

    private readonly WeatherService _weatherService = new();
    private readonly DispatcherTimer _weatherTimer = new() { Interval = TimeSpan.FromMinutes(10) };
    private readonly DispatcherTimer _phraseTimer = new() { Interval = TimeSpan.FromSeconds(9) };
    private readonly DispatcherTimer _watchTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _waveTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private WeatherNow? _weather;
    private int _phraseIndex = -1;

    private ToastWindow? _toast;
    private bool _autoHidden;
    private bool _asleep;
    private int _lastDownloads;
    private double _wavePhase;

    private static readonly (string Line, string Sub)[] Phrases =
    {
        ("привет", "я живу наверху экрана"),
        ("смотрю за музыкой", "и за твоим буфером тоже"),
        ("тут тихо", "включи что-нибудь"),
        ("дремлю", "но одним глазом"),
        ("всё под контролем", "почти"),
        ("люблю порядок", "на полке у меня файлы"),
        ("не забудь отдохнуть", "я подожду"),
    };

    public MainWindow()
    {
        Instance = this;
        _settings = App.Settings;
        Theme.SetLight(_settings.LightTheme);
        Theme.SetRim(_settings.LightRim);

        InitializeComponent();

        ApplyScale();
        ApplyPosition();
        ApplyLook();

        ClipboardStore.Load();
        ClipboardStore.Cleanup();
        ShelfStore.Load();
        NotesStore.Load();
        TimerService.Restore();
        BuildTiles();

        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!_userPinned) Collapse();
        };

        TimerService.Finished += () =>
        {
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch
            {
            }
            (Application.Current as App)?.Notify("Таймер", "Время вышло");
            UpdateIdleBar();
        };

        Loaded += async (_, _) =>
        {
            await _media.InitAsync();
            UpdateClock();
            UpdateVolume();
            _tick.Tick += async (_, _) => await RefreshAsync();
            _tick.Start();
            await RefreshAsync();

            if (!_hasMedia)
            {
                HeaderRow.Visibility = Visibility.Collapsed;
                IdleBar.Visibility = Visibility.Visible;
                Capsule.Width = Theme.IdleWidth;
                Capsule.Height = Theme.IdleHeight;
                Capsule.CornerRadius = new CornerRadius(Theme.IdleHeight / 2);
            }

            await LoadWeatherAsync();
            _weatherTimer.Tick += async (_, _) => await LoadWeatherAsync();
            _weatherTimer.Start();

            _phraseTimer.Tick += (_, _) => RotatePhrase();
            _phraseTimer.Start();
            RotatePhrase();

            _watchTimer.Tick += (_, _) => Watch();
            _watchTimer.Start();

            _waveTimer.Tick += (_, _) => UpdateLiquidEdge();
            _waveTimer.Start();

            var cleanup = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            cleanup.Tick += (_, _) => ClipboardStore.Cleanup();
            cleanup.Start();

            var remind = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            remind.Tick += (_, _) =>
            {
                foreach (var note in NotesStore.TakeDue())
                {
                    var text = note.Text.Length > 60 ? note.Text[..60] + "…" : note.Text;
                    ShowOsd("\uE7ED", "Напоминание", text, sticky: true);
                }
            };
            remind.Start();

            _ = CheckUpdateLaterAsync();
        };
    }

    private async Task LoadWeatherAsync()
    {
        _weather = await _weatherService.GetAsync(_settings.WeatherCity);
        UpdateIdleBar();
    }

    private void RotatePhrase()
    {
        if (Phrases.Length == 0) return;
        _phraseIndex = (_phraseIndex + 1) % Phrases.Length;
        var (line, sub) = Phrases[_phraseIndex];
        MascotPhrase.Text = line;
        MascotSub.Text = sub;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowHelper.MakeOverlay(this);

        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(handle);
        NativeMethods.RegisterHotKey(handle, HOTKEY_LAUNCHER, NativeMethods.MOD_ALT, NativeMethods.VK_SPACE);
        NativeMethods.RegisterHotKey(handle, HOTKEY_SHOT, NativeMethods.MOD_ALT, NativeMethods.VK_S);
        NativeMethods.RegisterHotKey(handle, HOTKEY_RECORD, NativeMethods.MOD_ALT, NativeMethods.VK_R);
        NativeMethods.RegisterHotKey(handle, HOTKEY_SHOT_ALT, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_S);
        NativeMethods.RegisterHotKey(handle, HOTKEY_RECORD_ALT, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, NativeMethods.VK_R);
        NativeMethods.RegisterHotKey(handle, HOTKEY_SEARCH, NativeMethods.MOD_CONTROL, NativeMethods.VK_K);
        NativeMethods.RegisterHotKey(handle, HOTKEY_NOTE, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_N);
        NativeMethods.RegisterHotKey(handle, HOTKEY_TRANSLATE, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, NativeMethods.VK_T);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (text.Length > 0 && text != _lastClip)
                    {
                        _lastClip = text;
                        ClipboardStore.Push(text);
                        Poke();
                        if (_settings.CopyToast && text.Length <= 400) ShowCopyToast(text);
                    }
                }
            }
            catch
            {
            }
        }
        else if (msg == WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HOTKEY_LAUNCHER:
                case HOTKEY_SEARCH:
                    OpenPanel(new LauncherPanel());
                    handled = true;
                    break;
                case HOTKEY_SHOT:
                case HOTKEY_SHOT_ALT:
                    StartScreenshot();
                    handled = true;
                    break;
                case HOTKEY_RECORD:
                case HOTKEY_RECORD_ALT:
                    ToggleRecording();
                    handled = true;
                    break;
                case HOTKEY_NOTE:
                    OpenTile("notes");
                    handled = true;
                    break;
                case HOTKEY_TRANSLATE:
                    _ = TranslateSelectionAsync();
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    // ── раскрытие ────────────────────────────────────────────────────────────

    private void OnCapsuleEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (_settings.ExpandOnHover) Expand();
    }

    private void OnCapsuleLeave(object sender, MouseEventArgs e)
    {
        if (_userPinned) return;
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    public void Expand()
    {
        if (_expanded || _animating) return;
        _expanded = true;
        IdleBar.Visibility = Visibility.Collapsed;
        if (_hasMedia) ShowHeader();
        Body.Visibility = Visibility.Visible;
        Animate(Theme.ExpandedWidth, Theme.ExpandedHeight, TimeSpan.FromMilliseconds(260),
            new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    /// <summary>Колесо над капсулой: вверх — раскрыть, вниз — свернуть.</summary>
    private void OnCapsuleWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (e.Delta > 0)
        {
            if (!_expanded) Expand();
        }
        else
        {
            if (_expanded) Collapse();
        }
    }

    public void Collapse()
    {
        if (!_expanded || _animating) return;
        _expanded = false;

        var width = _hasMedia ? Theme.CollapsedWidth : Theme.IdleWidth;
        var height = _hasMedia ? Theme.CollapsedHeight : Theme.IdleHeight;
        if (!_hasMedia) FadeHeader(false);

        Animate(width, height, TimeSpan.FromMilliseconds(220),
            new CubicEase { EasingMode = EasingMode.EaseIn }, onDone: () =>
            {
                if (_expanded) return;
                Body.Visibility = Visibility.Collapsed;
                ClosePanel();
                if (!_hasMedia)
                {
                    IdleBar.Visibility = Visibility.Visible;
                    UpdateIdleBar();
                }
            });
    }

    /// <summary>Полоска без музыки: сворачиваем капсулу в тонкую полосу у верхней кромки.</summary>
    private void ApplyIdleShape()
    {
        if (_expanded) return;
        if (_hasMedia)
        {
            ShowHeader();
            Animate(Theme.CollapsedWidth, Theme.CollapsedHeight, TimeSpan.FromMilliseconds(240),
                new CubicEase { EasingMode = EasingMode.EaseOut });
        }
        else
        {
            FadeHeader(false);
            Animate(Theme.IdleWidth, Theme.IdleHeight, TimeSpan.FromMilliseconds(240),
                new CubicEase { EasingMode = EasingMode.EaseIn });
        }
    }

    private void ShowHeader()
    {
        IdleBar.Visibility = Visibility.Collapsed;
        if (HeaderRow.Visibility == Visibility.Visible && HeaderRow.Opacity >= 0.99) return;
        HeaderRow.Visibility = Visibility.Visible;
        HeaderRow.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
    }

    private void FadeHeader(bool show)
    {
        if (show)
        {
            ShowHeader();
            return;
        }

        IdleBar.Visibility = Visibility.Visible;
        UpdateIdleBar();

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) =>
        {
            if (!_hasMedia || !_expanded) HeaderRow.Visibility = Visibility.Collapsed;
        };
        HeaderRow.BeginAnimation(OpacityProperty, fade);
    }

    private void UpdateIdleBar()
    {
        // пока идёт таймер, на полосе вместо погоды показываем его остаток
        if (TimerService.ShowOnStrip)
        {
            IdleWeatherIcon.Kind = "clock";
            IdleTemp.Text = TimerService.Text;
            return;
        }

        if (_weather is null)
        {
            IdleWeatherIcon.Kind = "cloud";
            IdleTemp.Text = "—";
            return;
        }

        IdleWeatherIcon.Kind = _weather.Kind;
        IdleTemp.Text = $"{Math.Round(_weather.Temp)}°";
    }

    private void Animate(double w, double h, TimeSpan duration, IEasingFunction ease, Action? onDone = null)
    {
        _animating = true;
        var dw = new DoubleAnimation(w, duration) { EasingFunction = ease };
        var dh = new DoubleAnimation(h, duration) { EasingFunction = ease };
        dh.Completed += (_, _) =>
        {
            _animating = false;
            onDone?.Invoke();
        };
        Capsule.BeginAnimation(WidthProperty, dw);
        Capsule.BeginAnimation(HeightProperty, dh);

        // Полоса — пилюля (радиус = половина высоты), раскрытая панель — обычное скругление.
        // CornerRadius нельзя анимировать DoubleAnimation, поэтому идём дискретными кадрами.
        AnimateRadius(Capsule.CornerRadius.TopLeft, RadiusFor(h), duration);
    }

    private static double RadiusFor(double height) => Math.Min(height / 2, Theme.Radius);

    private void AnimateRadius(double from, double to, TimeSpan duration)
    {
        if (Math.Abs(from - to) < 0.5)
        {
            Capsule.BeginAnimation(Border.CornerRadiusProperty, null);
            Capsule.CornerRadius = new CornerRadius(to);
            return;
        }

        var frames = new ObjectAnimationUsingKeyFrames();
        const int steps = 14;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            frames.KeyFrames.Add(new DiscreteObjectKeyFrame(
                new CornerRadius(from + (to - from) * t),
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(duration.TotalMilliseconds * t))));
        }
        Capsule.BeginAnimation(Border.CornerRadiusProperty, frames);
    }

    // ── плитки и настройки ───────────────────────────────────────────────────

    private void BuildTiles()
    {
        TileGrid.Children.Clear();
        var style = (Style)FindResource("Tile");
        foreach (var def in Tiles.All)
        {
            if (!_settings.HomeTiles.Contains(def.Id)) continue;
            var button = Tiles.Create(def, style, OnTile, out var label);
            if (def.Id == "record") _recordLabel = label;
            TileGrid.Children.Add(button);
        }
        TileGrid.Columns = TileGrid.Children.Count <= 4 ? 2 : 3;
    }

    public void ApplySettings()
    {
        _settings = App.Settings;
        Theme.SetLight(_settings.LightTheme);
        Theme.SetRim(_settings.LightRim);
        ApplyScale();
        ApplyPosition();
        ApplyLook();
        BuildTiles();
        UpdateClock();
        if (!_settings.ShowOsd) _osd?.HideNow();
    }

    /// <summary>Размер всего острова: 100–199 % — для больших мониторов.</summary>
    private void ApplyScale()
    {
        var scale = Math.Clamp(_settings.Scale <= 0 ? 1 : _settings.Scale, 1, 1.99);
        UiScale.ScaleX = scale;
        UiScale.ScaleY = scale;
        Width = 920 * scale;
        Height = 640 * scale;
    }

    private void ApplyPosition()
    {
        if (_settings.Detached && _settings.WindowLeft >= 0 && _settings.WindowTop >= 0)
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
            return;
        }

        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    private void ApplyLook()
    {
        try
        {
            FontFamily = new FontFamily(_settings.UiFont);
        }
        catch
        {
        }

        Capsule.Effect = _settings.ShowShadow ? CapsuleShadow : null;

        foreach (var mascot in new[] { IdleMascot, MusicMascot, BigMascot })
        {
            mascot.FollowCursor = _settings.FollowCursor;
            mascot.Calm = _settings.ReduceMotion;
        }

        if (!_settings.ShowPrivacyDots)
        {
            MicDot.Visibility = Visibility.Collapsed;
            CamDot.Visibility = Visibility.Collapsed;
            IdleMicDot.Visibility = Visibility.Collapsed;
            IdleCamDot.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowOsd(string glyph, string title, string value, bool sticky = false)
    {
        if (!_settings.ShowOsd) return;
        _osd ??= new OsdWindow();
        _osd.ShowOsd(glyph, title, value, sticky);
    }

    private async void ToggleRecording()
    {
        if (_recorder.IsRecording)
        {
            await _recorder.StopAsync();
            if (_recordLabel is not null) _recordLabel.Text = "Запись";
            ShowOsd("\uE7C8", "Запись экрана", "сохранена");
            (Application.Current as App)?.Notify("Запись экрана", _recorder.LastFile ?? "");
        }
        else
        {
            var folder = string.IsNullOrWhiteSpace(_settings.RecordFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Kapsula")
                : _settings.RecordFolder;
            _recorder.Start(folder, _settings.RecordSystemAudio);
            if (_recordLabel is not null) _recordLabel.Text = "Стоп";
            ShowOsd("\uE7C8", "Запись экрана", "идёт");
            (Application.Current as App)?.Notify("Запись экрана", "Пишу экран. Alt+R — остановить");
        }
    }

    // ── панели ───────────────────────────────────────────────────────────────

    private void OnTile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }) return;
        OpenTile(tag);
    }

    /// <summary>Открыть раздел по имени плитки — используется и лаунчером.</summary>
    public void OpenTile(string tag)
    {
        switch (tag)
        {
            case "clipboard":
                OpenPanel(new ClipboardPanel());
                break;
            case "shelf":
                OpenPanel(new ShelfPanel());
                break;
            case "notes":
                OpenPanel(new NotesPanel());
                break;
            case "ai":
                OpenPanel(new AiPanel());
                break;
            case "weather":
                OpenPanel(new WeatherPanel());
                break;
            case "shot":
                StartScreenshot();
                break;
            case "quick":
                OpenPanel(new QuickPanel());
                break;
            case "timer":
                OpenPanel(new TimerPanel());
                break;
            case "record":
                ToggleRecording();
                break;
            case "summary":
                ShowSummary();
                break;
            case "appearance":
                OpenPanel(new AppearancePanel());
                break;
            case "system":
                OpenPanel(new SystemPanel());
                break;
            case "music":
                OpenPanel(new MusicPanel());
                break;
        }
    }

    private void OpenPanel(FrameworkElement panel)
    {
        switch (panel)
        {
            case ClipboardPanel p: p.Back += ClosePanel; break;
            case ShelfPanel p: p.Back += ClosePanel; break;
            case NotesPanel p: p.Back += ClosePanel; break;
            case AiPanel p: p.Back += ClosePanel; break;
            case WeatherPanel p: p.Back += ClosePanel; break;
            case LauncherPanel p: p.Back += ClosePanel; break;
            case QuickPanel p: p.Back += ClosePanel; break;
            case TimerPanel p: p.Back += ClosePanel; break;
            case AppearancePanel p: p.Back += ClosePanel; break;
            case SystemPanel p: p.Back += ClosePanel; break;
            case MusicPanel p: p.Back += ClosePanel; break;
        }

        PanelHost.Content = panel;
        PanelHost.Visibility = Visibility.Visible;
        HomeView.Visibility = Visibility.Collapsed;

        // панель не появляется рывком: короткое проявление снизу вверх
        if (PanelHost.RenderTransform is not TranslateTransform slide)
        {
            slide = new TranslateTransform();
            PanelHost.RenderTransform = slide;
        }

        PanelHost.BeginAnimation(OpacityProperty, null);
        PanelHost.Opacity = 0;
        PanelHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        Expand();
        Activate();
    }

    private void ClosePanel()
    {
        PanelHost.BeginAnimation(OpacityProperty, null);
        PanelHost.Opacity = 1;
        PanelHost.Content = null;
        PanelHost.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
    }

    private void StartScreenshot()
    {
        Hide();
        var overlay = new RegionOverlay();
        overlay.Finished += shot =>
        {
            Show();
            if (shot is null) return;

            // снимок сразу в буфер, редактор открывается как обычно
            try
            {
                Clipboard.SetImage(shot);
            }
            catch
            {
            }

            var editor = new ScreenshotEditor(shot);
            editor.Show();
            editor.Activate();
        };
        overlay.Show();
        overlay.Activate();
    }

    /// <summary>Единственное окно капсулы — нужно командам из чата (скриншот, тема).</summary>
    public static MainWindow? Instance { get; private set; }

    /// <summary>Снимок экрана по просьбе из чата ИИ.</summary>
    public void TakeScreenshot() => StartScreenshot();

    /// <summary>Светлая или тёмная тема по просьбе из чата ИИ.</summary>
    public void SetLightTheme(bool light)
    {
        App.Settings.LightTheme = light;
        App.Settings.Save();
        ApplySettings();
    }

    // ── обновление данных ────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        UpdateClock();
        if (!_volumeDragging) UpdateVolume();
        TimerService.Update();
        UpdateIdleBar();
        CheckSummary();
        await UpdateMediaAsync();
    }

    private DateTime _lastUpdateCheck;

    private async Task CheckUpdateLaterAsync()
    {
        // не мешаем старту: спрашиваем GitHub через полминуты и потом раз в сутки
        await Task.Delay(TimeSpan.FromSeconds(30));
        if ((DateTime.UtcNow - _lastUpdateCheck).TotalHours < 24 && _lastUpdateCheck != default) return;
        _lastUpdateCheck = DateTime.UtcNow;
        await CheckUpdateAsync();
    }

    private async Task CheckUpdateAsync()
    {
        if (_update is not null) return;
        _update = await Updater.CheckAsync();
        if (_update is null) return;

        _updateStrip ??= new SummaryStripWindow();
        _updateStrip.Opened -= OnUpdateStripOpened;
        _updateStrip.Opened += OnUpdateStripOpened;
        var bounds = CapsuleScreenBounds();
        _updateStrip.ShowNear(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            $"доступна версия {_update.Version}", "\uE895");
    }

    private async void OnUpdateStripOpened()
    {
        if (_update is null) return;

        var exe = Environment.ProcessPath ?? "";
        ShowOsd("\uE896", "Обновление", "скачиваю");

        var lastShown = -10;
        var file = await Updater.DownloadAsync(_update, percent =>
        {
            if (percent / 10 == lastShown / 10) return;
            lastShown = percent;
            ShowOsd("\uE896", "Обновление", $"{percent}%", sticky: true);
        }, CancellationToken.None);

        if (file is null)
        {
            ShowOsd("\uE783", "Обновление", "не удалось скачать");
            (Application.Current as App)?.Notify("Обновление", "Не получилось скачать новую версию");
            return;
        }

        if (Updater.CanReplaceInPlace(exe))
        {
            ShowOsd("\uE895", "Обновление", "перезапускаюсь");
            Updater.ScheduleReplace(file, exe);
            Application.Current.Shutdown();
            return;
        }

        var saved = Updater.MoveToDownloads(file, _update.Version);
        (Application.Current as App)?.Notify("Обновление", "Новая версия сохранена в «Загрузки»");
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{saved}\"") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    // ── сводка дня ───────────────────────────────────────────────────────────

    private SummaryStripWindow? _summaryStrip;
    private SummaryWindow? _summaryWindow;

    private void CheckSummary()
    {
        if (!_settings.SummaryEnabled) return;

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_settings.SummaryShownOn == today) return;

        if (!TimeSpan.TryParse(_settings.SummaryTime, out var at)) at = new TimeSpan(9, 0, 0);
        if (DateTime.Now.TimeOfDay < at) return;

        _settings.SummaryShownOn = today;
        _settings.Save();
        ShowSummaryStrip();
    }

    /// <summary>Где капсула реально на экране: окно большое и прозрачное, а плашки нужны у самой капсулы.</summary>
    private (double Left, double Top, double Width, double Height) CapsuleScreenBounds()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var topLeft = Capsule.PointToScreen(new Point(0, 0));
        var bottomRight = Capsule.PointToScreen(new Point(Capsule.ActualWidth, Capsule.ActualHeight));
        return (
            topLeft.X / dpi.DpiScaleX,
            topLeft.Y / dpi.DpiScaleY,
            (bottomRight.X - topLeft.X) / dpi.DpiScaleX,
            (bottomRight.Y - topLeft.Y) / dpi.DpiScaleY);
    }

    private void ShowSummaryStrip()
    {
        _summaryStrip ??= new SummaryStripWindow();
        _summaryStrip.Opened -= ShowSummary;
        _summaryStrip.Opened += ShowSummary;
        var bounds = CapsuleScreenBounds();
        _summaryStrip.ShowNear(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public void ShowSummary()
    {
        _summaryStrip?.HideNow();
        if (_summaryWindow is { IsVisible: true })
        {
            _summaryWindow.Activate();
            return;
        }
        _summaryWindow = new SummaryWindow(_settings);
        _summaryWindow.ShowOnRight();
    }

    private DateTime _lastPrivacyCheck;
    private bool? _lastCharging;
    private bool _fullShown;

    /// <summary>Ctrl+Alt+T: копируем выделенное, переводим на русский и кладём перевод в буфер.</summary>
    private async Task TranslateSelectionAsync()
    {
        string? before = null;
        try
        {
            if (Clipboard.ContainsText()) before = Clipboard.GetText();
        }
        catch
        {
        }

        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_C_LETTER, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_C_LETTER, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

        await Task.Delay(250);

        var picked = "";
        try
        {
            if (Clipboard.ContainsText()) picked = Clipboard.GetText();
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(picked) || picked == before || picked.Length > 4000)
        {
            ShowOsd("\uE8C8", "Перевод", "выдели текст");
            return;
        }

        ShowOsd("\uE8C8", "Перевод", "перевожу…", sticky: true);
        var result = await Services.Translator.ToRussianAsync(picked);
        if (result is null)
        {
            ShowOsd("\uE8C8", "Перевод", "не получилось");
            return;
        }

        try
        {
            Clipboard.SetText(result);
        }
        catch
        {
        }

        ShowOsd("\uE8C8", "Перевод", result.Length > 70 ? result[..70] + "…" : result, sticky: true);
    }

    private void CheckIndicators()
    {
        var volume = VolumeService.Get();
        if (_lastVolume >= 0 && Math.Abs(volume - _lastVolume) > 0.01)
            ShowOsd("\uE767", "Громкость", $"{(int)Math.Round(volume * 100)}%");
        _lastVolume = volume;

        var caps = Console.CapsLock;
        if (caps != _lastCaps)
            ShowOsd("\uE8F6", "Caps Lock", caps ? "включён" : "выключен");
        _lastCaps = caps;

        // включили или вынули зарядку — короткая плашка в стиле капсулы
        var (battery, charging, hasBattery) = Services.SysInfo.Battery();
        if (hasBattery && _lastCharging is { } was && was != charging)
            ShowOsd(charging ? "\uE83E" : "\uE83F", "Батарея",
                charging ? $"заряжается, {battery}%" : $"от сети отключена, {battery}%");
        if (hasBattery && charging && battery >= 100 && _fullShown == false)
        {
            ShowOsd("\uE73E", "Батарея", "заряжена");
            _fullShown = true;
        }
        if (!charging) _fullShown = false;
        if (hasBattery) _lastCharging = charging;

        foreach (var mascot in new[] { IdleMascot, MusicMascot, BigMascot })
            mascot.Scared = caps;

        // когда курсор не ведёт маскота, глаза следят за ползунком громкости
        if (!_settings.FollowCursor)
        {
            var nx = Math.Clamp(volume * 2 - 1, -1, 1);
            IdleMascot.LookAt(nx, 0.25);
            MusicMascot.LookAt(nx, 0.25);
            BigMascot.LookAt(nx, 0.25);
        }

        if ((DateTime.UtcNow - _lastPrivacyCheck).TotalMilliseconds < 1500) return;
        _lastPrivacyCheck = DateTime.UtcNow;

        var mic = _settings.ShowPrivacyDots && DesktopWatch.MicInUse();
        var cam = _settings.ShowPrivacyDots && DesktopWatch.CameraInUse();
        var micVisibility = mic ? Visibility.Visible : Visibility.Collapsed;
        var camVisibility = cam ? Visibility.Visible : Visibility.Collapsed;
        MicDot.Visibility = micVisibility;
        IdleMicDot.Visibility = micVisibility;
        CamDot.Visibility = camVisibility;
        IdleCamDot.Visibility = camVisibility;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var time = now.ToString(_settings.ShowSeconds ? "HH:mm:ss" : "HH:mm");
        ClockText.Text = time;
        IdleTime.Text = time;
        MascotTime.Text = time;
        MascotDate.Text = now.ToString("ddd, d MMMM", new System.Globalization.CultureInfo("ru-RU"));
    }

    private void UpdateVolume()
    {
        var v = VolumeService.Get();
        var muted = VolumeService.IsMuted();
        var ratio = muted ? 0 : Math.Clamp(v, 0f, 1f);
        VolumeFillTrack.Width = Math.Max(0, VolumeTrack.ActualWidth * ratio);
        VolumeIcon.Text = muted || v <= 0.001 ? "\uE74F" : "\uE767";
        VolumePercent.Text = muted ? "выкл" : $"{(int)Math.Round(v * 100)}%";
    }

    private async Task UpdateMediaAsync()
    {
        var info = await _media.ReadAsync();
        if (info is not null) _lastMediaOk = DateTime.UtcNow;
        else if (_hasMedia && (DateTime.UtcNow - _lastMediaOk).TotalSeconds < 2) return; // сбой чтения — не мигаем
        _current = info;

        var hasMedia = info is not null && info.HasSession && info.IsPlaying;
        if (hasMedia != _hasMedia)
        {
            _hasMedia = hasMedia;
            ApplyIdleShape();
        }

        // страховка: если музыка играет, шапка обязана быть на экране
        if (_hasMedia && !_expanded && HeaderRow.Visibility != Visibility.Visible)
        {
            HeaderRow.BeginAnimation(OpacityProperty, null);
            HeaderRow.Opacity = 1;
            HeaderRow.Visibility = Visibility.Visible;
            IdleBar.Visibility = Visibility.Collapsed;
        }

        MusicMascot.Dancing = hasMedia;
        MusicMascot.WearingHeadphones = info is not null && info.HasSession;

        if (info is null || !info.HasSession)
        {
            TitleText.Text = "Ничего не играет";
            ArtistText.Text = "включи музыку — появится здесь";
            TitleBig.Text = "Ничего не играет";
            ArtistBig.Text = "включи музыку";
            PlayPauseText.Text = "\uE768";
            ArtPill.Background = Theme.AccentGradient;
            ArtBig.Background = Theme.AccentGradient;
            ArtFallback.Visibility = Visibility.Visible;
            ArtBigFallback.Visibility = Visibility.Visible;
            return;
        }

        TitleText.Text = info.Title;
        ArtistText.Text = string.IsNullOrWhiteSpace(info.Artist) ? info.App : $"{info.Artist} · {info.App}";
        TitleBig.Text = info.Title;
        ArtistBig.Text = string.IsNullOrWhiteSpace(info.Artist) ? info.App : info.Artist;
        PlayPauseText.Text = info.IsPlaying ? "\uE769" : "\uE768";

        if (info.Art is not null)
        {
            var image = Theme.LoadImage(info.Art);
            if (image is not null)
            {
                var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                brush.Freeze();
                ArtPill.Background = brush;
                ArtBig.Background = brush;
                ArtFallback.Visibility = Visibility.Collapsed;
                ArtBigFallback.Visibility = Visibility.Collapsed;
            }
        }
    }

    // ── управление ───────────────────────────────────────────────────────────

    private async void OnPlayPause(object sender, RoutedEventArgs e)
    {
        await _media.PlayPauseAsync();
        await Task.Delay(120);
        await UpdateMediaAsync();
    }

    private async void OnNext(object sender, RoutedEventArgs e)
    {
        await _media.NextAsync();
        await Task.Delay(200);
        await UpdateMediaAsync();
    }

    private async void OnPrevious(object sender, RoutedEventArgs e)
    {
        await _media.PreviousAsync();
        await Task.Delay(200);
        await UpdateMediaAsync();
    }

    private void OnVolumeDown(object sender, RoutedEventArgs e) => NudgeVolume(-_settings.VolumeStep / 100f);

    private void OnVolumeUp(object sender, RoutedEventArgs e) => NudgeVolume(_settings.VolumeStep / 100f);

    private void NudgeVolume(float delta)
    {
        VolumeService.Set(VolumeService.Get() + delta);
        UpdateVolume();
    }

    private void OnVolumeStartDrag(object sender, MouseButtonEventArgs e)
    {
        _volumeDragging = true;
        SetVolumeFromMouse(e);
    }

    private void OnVolumeDrag(object sender, MouseEventArgs e)
    {
        if (_volumeDragging) SetVolumeFromMouse(e);
    }

    private void OnVolumeEndDrag(object sender, MouseButtonEventArgs e) => _volumeDragging = false;

    private void SetVolumeFromMouse(MouseEventArgs e)
    {
        var x = e.GetPosition(VolumeTrack).X;
        var ratio = Math.Clamp(x / Math.Max(1, VolumeTrack.ActualWidth), 0, 1);
        VolumeService.Set((float)ratio);
        UpdateVolume();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        _userPinned = !_userPinned;
        PinButton.Opacity = _userPinned ? 1 : 0.45;
        if (_userPinned) Expand();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        (Application.Current as App)?.OpenSettings();

    // ── остров где угодно ────────────────────────────────────────────────────

    private void OnCapsuleDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (IsInteractive(e.OriginalSource as DependencyObject)) return;

        if (e.ClickCount == 2)
        {
            OnCapsuleDoubleClick(sender, e);
            return;
        }

        _dragging = true;
        _dragFrom = PointToScreen(e.GetPosition(this));
        Capsule.CaptureMouse();
        e.Handled = true;
    }

    private void OnCapsuleMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var now = PointToScreen(e.GetPosition(this));
        Left += now.X - _dragFrom.X;
        Top += now.Y - _dragFrom.Y;
        _dragFrom = now;
    }

    private void OnCapsuleUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Capsule.ReleaseMouseCapture();

        _settings.Detached = true;
        SnapToEdges();
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
    }

    private bool _dragging;
    private Point _dragFrom;

    private void OnCapsuleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindButton(e.OriginalSource as DependencyObject) is not null) return;
        _settings.Detached = false;
        _settings.WindowLeft = -1;
        _settings.WindowTop = -1;
        _settings.Save();
        ApplyPosition();
    }

    /// <summary>Прилипание к краям экрана: капсула пружинит к кромке.</summary>
    private void SnapToEdges()
    {
        const double snap = 36;
        var screenW = SystemParameters.PrimaryScreenWidth;
        var capsuleW = Capsule.ActualWidth * Math.Max(1, _settings.Scale);
        var capsuleLeft = Left + (Width - capsuleW) / 2;

        if (Math.Abs(capsuleLeft) < snap) Left -= capsuleLeft;
        else if (Math.Abs(screenW - (capsuleLeft + capsuleW)) < snap) Left += screenW - (capsuleLeft + capsuleW);

        if (Math.Abs(Top) < snap) Top = 0;
        if (Top < 0) Top = 0;
        if (Top > SystemParameters.PrimaryScreenHeight - 60) Top = SystemParameters.PrimaryScreenHeight - 60;
    }

    private static Button? FindButton(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is Button button) return button;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private bool IsInteractive(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is Button or TextBoxBase or ListBox or ListBoxItem or ScrollViewer
                or Slider or CheckBox or ComboBox)
                return true;
            if (ReferenceEquals(node, VolumeTrack)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    // ── живой маскот ─────────────────────────────────────────────────────────

    /// <summary>Ненадолго радуется: скопировал, докачалось.</summary>
    private void Poke(double seconds = 1.6)
    {
        var all = new[] { IdleMascot, MusicMascot, BigMascot };
        foreach (var mascot in all) mascot.Excited = true;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var mascot in all) mascot.Excited = false;
        };
        timer.Start();
    }

    private void ShowCopyToast(string text)
    {
        _toast ??= new ToastWindow();
        _toast.ShowText(text);
    }

    // ── что происходит вокруг ────────────────────────────────────────────────

    private void Watch()
    {
        CheckIndicators();
        CheckSleep();
        CheckDownloads();
        CheckFullscreen();
    }

    private void CheckSleep()
    {
        var asleep = DesktopWatch.IdleMilliseconds() > 120_000;
        if (asleep == _asleep) return;

        _asleep = asleep;
        foreach (var mascot in new[] { IdleMascot, MusicMascot, BigMascot })
            mascot.Asleep = asleep;

        if (!asleep)
        {
            Poke(1.2);
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
            }
        }
    }

    private void CheckDownloads()
    {
        var downloads = SystemToggles.ActiveDownloads();
        if (downloads > _lastDownloads && downloads > 0)
            ShowOsd("\uE896", "Загрузка", downloads == 1 ? "качается файл" : $"файлов: {downloads}");

        if (downloads == 0 && _lastDownloads > 0)
        {
            Poke();
            ShowOsd("\uE73E", "Загрузка", "готова");
        }
        _lastDownloads = downloads;
    }

    /// <summary>Видео, игра или полноэкранный браузер — остров уходит с дороги.</summary>
    private void CheckFullscreen()
    {
        if (!_settings.HideInFullscreen || _expanded || _userPinned)
        {
            ShowFromAutoHide();
            return;
        }

        var own = new WindowInteropHelper(this).Handle;
        if (DesktopWatch.ForegroundIsFullscreen(own)) AutoHide();
        else ShowFromAutoHide();
    }

    private void AutoHide()
    {
        if (_autoHidden) return;
        _autoHidden = true;
        IsHitTestVisible = false;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
    }

    private void ShowFromAutoHide()
    {
        if (!_autoHidden) return;
        _autoHidden = false;
        IsHitTestVisible = true;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
    }

    /// <summary>Волна по нижней кромке капсулы: чем громче музыка, тем выше.</summary>
    private void UpdateLiquidEdge()
    {
        if (!_hasMedia || _settings.ReduceMotion || !_settings.ShowLiquidEdge)
        {
            LiquidEdge.Opacity = 0;
            return;
        }

        var w = LiquidLayer.ActualWidth;
        var h = LiquidLayer.ActualHeight;
        if (w <= 0 || h <= 0) return;

        ClipLiquid(w, h);

        _wavePhase += 0.26;
        var volume = VolumeService.IsMuted() ? 0 : VolumeService.Get();
        var amp = 2.6 + volume * 5.0;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);
            for (double x = 0; x <= w; x += 6)
            {
                var y = h - 1.2 - amp * (0.5 + 0.5 * Math.Sin(x / 26 + _wavePhase));
                ctx.LineTo(new Point(x, y), true, false);
            }
            ctx.LineTo(new Point(w, h), true, false);
        }
        geometry.Freeze();

        LiquidEdge.Data = geometry;
        LiquidEdge.Opacity = 0.32;
    }

    private double _clipW = -1;
    private double _clipH = -1;
    private double _clipR = -1;

    /// <summary>Волна не должна вылезать за скруглённые углы капсулы.</summary>
    private void ClipLiquid(double w, double h)
    {
        var r = Math.Max(0, Capsule.CornerRadius.TopLeft - 1);
        if (Math.Abs(w - _clipW) < 0.5 && Math.Abs(h - _clipH) < 0.5 && Math.Abs(r - _clipR) < 0.5) return;

        _clipW = w;
        _clipH = h;
        _clipR = r;

        var clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        clip.Freeze();
        LiquidLayer.Clip = clip;
    }

    // ── перетаскивание файлов ────────────────────────────────────────────────

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        Expand();
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            ShelfStore.Add(paths);
            OpenPanel(new ShelfPanel());
        }
    }
}
