using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kapsula.Services;
using Windows.Devices.Radios;

namespace Kapsula.Panels;

public partial class QuickPanel : UserControl
{
    public event Action? Back;

    private bool _dragging;
    private int _pendingLight = -1;
    private int _lightValue = 100;
    private readonly DispatcherTimer _lightTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };

    public QuickPanel()
    {
        InitializeComponent();
        _lightTimer.Tick += (_, _) =>
        {
            _lightTimer.Stop();
            if (_pendingLight < 0) return;
            ScreenBrightness.Set(_pendingLight);
            App.Settings.ScreenBrightness = _pendingLight;
            _saveTimer.Stop();
            _saveTimer.Start();
            _pendingLight = -1;
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            App.Settings.Save();
        };
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var wifi = await SystemToggles.RadioStateAsync(RadioKind.WiFi);
        WifiState.Text = wifi is null ? "нет адаптера" : wifi.Value ? "включён" : "выключен";

        var bt = await SystemToggles.RadioStateAsync(RadioKind.Bluetooth);
        BtState.Text = bt is null ? "нет адаптера" : bt.Value ? "включён" : "выключен";

        ThemeState.Text = SystemToggles.IsLightTheme() ? "светлая" : "тёмная";

        UpdateSound();
        UpdateLight();

        var downloads = SystemToggles.ActiveDownloads();
        DownloadsText.Text = downloads == 0 ? "Активных загрузок нет" : $"Скачивается файлов: {downloads}";
    }

    private void UpdateSound()
    {
        var v = VolumeService.Get();
        var muted = VolumeService.IsMuted();
        var ratio = muted ? 0 : Math.Clamp(v, 0f, 1f);
        SoundFill.Width = Math.Max(0, SoundTrack.ActualWidth * ratio);
        SoundIcon.Text = muted || v <= 0.001 ? "\uE74F" : "\uE767";
        SoundState.Text = muted ? "выкл" : $"{(int)Math.Round(v * 100)}%";
    }

    private void UpdateLight()
    {
        var value = ScreenBrightness.Get();
        LightFill.Width = Math.Max(0, LightTrack.ActualWidth * value / 100.0);
        LightState.Text = $"{value}%";
        LightState.ToolTip = ScreenBrightness.IsHardware
            ? "Яркость подсветки экрана"
            : "Панель не даёт менять подсветку — затемняю картинку программно";
    }

    // общий ползунок для звука и яркости: тянем мышью по дорожке
    private void OnSliderDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement track) return;
        _dragging = true;
        track.CaptureMouse();
        SetFromPointer(track, e.GetPosition(track).X);
    }

    private void OnSliderMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || sender is not FrameworkElement track) return;
        SetFromPointer(track, e.GetPosition(track).X);
    }

    private void OnSliderUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        if (sender is not FrameworkElement track) return;
        track.ReleaseMouseCapture();

        if (track.Tag as string == "light")
        {
            App.Settings.ScreenBrightness = _lightValue;
            App.Settings.Save();
        }
    }

    private void SetFromPointer(FrameworkElement track, double x)
    {
        var ratio = Math.Clamp(x / Math.Max(1, track.ActualWidth), 0, 1);

        if (track.Tag as string == "sound")
        {
            VolumeService.Set((float)ratio);
            UpdateSound();
            return;
        }

        // яркость в ноль не опускаем — иначе экран гаснет и панель не вернуть мышью
        var percent = (int)Math.Round(ScreenBrightness.MinPercent + ratio * (100 - ScreenBrightness.MinPercent));
        LightFill.Width = Math.Max(0, track.ActualWidth * ratio);
        LightState.Text = $"{percent}%";
        _lightValue = percent;
        _pendingLight = percent;
        if (!_lightTimer.IsEnabled) _lightTimer.Start();
    }

    private async void OnWifi(object sender, RoutedEventArgs e)
    {
        await SystemToggles.ToggleRadioAsync(RadioKind.WiFi);
        await RefreshAsync();
    }

    private async void OnBluetooth(object sender, RoutedEventArgs e)
    {
        await SystemToggles.ToggleRadioAsync(RadioKind.Bluetooth);
        await RefreshAsync();
    }

    private void OnTheme(object sender, RoutedEventArgs e)
    {
        SystemToggles.SetTheme(!SystemToggles.IsLightTheme());
        _ = RefreshAsync();
    }

    private void OnMute(object sender, RoutedEventArgs e)
    {
        VolumeService.ToggleMute();
        _ = RefreshAsync();
    }

    private void OnOpenDownloads(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
            { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private static readonly Dictionary<string, string> PowerLabels = new()
    {
        ["lock"] = "Блокировка",
        ["sleep"] = "Сон",
        ["reboot"] = "Перезагрузка",
        ["shutdown"] = "Выключение",
    };

    private string? _armed;
    private DateTime _armedUntil;

    /// <summary>Сон/перезагрузка/выключение требуют второго нажатия — чтобы не нажать случайно.</summary>
    private void OnPower(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string kind) return;

        if (kind == "lock")
        {
            SystemToggles.LockScreen();
            return;
        }

        var label = PowerLabels[kind];
        if (_armed == kind && DateTime.UtcNow < _armedUntil)
        {
            button.Content = label;
            button.ClearValue(ForegroundProperty);
            _armed = null;
            Run(kind);
            return;
        }

        _armed = kind;
        _armedUntil = DateTime.UtcNow.AddSeconds(3);
        button.Content = "Нажми ещё раз";
        button.Foreground = Theme.AccentBrush;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = label;
            button.ClearValue(ForegroundProperty);
            if (_armed == kind) _armed = null;
        };
        timer.Start();
    }

    private static void Run(string kind)
    {
        switch (kind)
        {
            case "sleep": SystemToggles.Sleep(); break;
            case "reboot": SystemToggles.Reboot(); break;
            case "shutdown": SystemToggles.Shutdown(); break;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
