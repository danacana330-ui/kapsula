using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kapsula.Services;
using Windows.Devices.Radios;

namespace Kapsula.Panels;

public partial class QuickPanel : UserControl
{
    public event Action? Back;

    public QuickPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var wifi = await SystemToggles.RadioStateAsync(RadioKind.WiFi);
        WifiState.Text = wifi is null ? "нет адаптера" : wifi.Value ? "включён" : "выключен";

        var bt = await SystemToggles.RadioStateAsync(RadioKind.Bluetooth);
        BtState.Text = bt is null ? "нет адаптера" : bt.Value ? "включён" : "выключен";

        ThemeState.Text = SystemToggles.IsLightTheme() ? "светлая" : "тёмная";

        var muted = VolumeService.IsMuted();
        SoundState.Text = muted ? "выключен" : $"{(int)Math.Round(VolumeService.Get() * 100)}%";

        var downloads = SystemToggles.ActiveDownloads();
        DownloadsText.Text = downloads == 0 ? "Активных загрузок нет" : $"Скачивается файлов: {downloads}";
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
