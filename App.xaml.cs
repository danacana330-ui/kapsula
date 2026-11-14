using System.IO;
using System.Windows;
using System.Windows.Threading;
using Kapsula.Services;
using WinForms = System.Windows.Forms;

namespace Kapsula;

public partial class App : Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _window;

    public static AppSettings Settings { get; private set; } = new();

    public void Notify(string title, string text)
    {
        try
        {
            _tray?.ShowBalloonTip(4000, title, text, WinForms.ToolTipIcon.Info);
        }
        catch
        {
        }
    }

    /// <summary>Короткая строка состояния в подсказке трея: прогресс скачивания и т.п.</summary>
    public void SetTrayStatus(string? text)
    {
        try
        {
            if (_tray is null) return;
            var value = string.IsNullOrWhiteSpace(text) ? "Kapsula" : text!;
            _tray.Text = value.Length > 63 ? value[..63] : value;
        }
        catch
        {
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "Kapsula.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Settings = AppSettings.Load();
        Autostart.Apply(Settings.StartWithWindows);

        _window = new MainWindow();
        _window.SourceInitialized += (_, _) => WindowHelper.MakeOverlay(_window);
        _window.Show();

        CreateTray();

        DispatcherUnhandledException += (_, args) =>
        {
            // оверлей не должен падать от единичного сбоя системного API
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "kapsula_err.txt"),
                    args.Exception.ToString() + "\n---\n");
            }
            catch
            {
            }
            args.Handled = true;
        };
    }

    private void CreateTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Kapsula",
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Показать / скрыть", null, (_, _) => ToggleWindow());
        menu.Items.Add("Раскрыть", null, (_, _) => _window?.Expand());
        menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ToggleWindow();
    }

    public void OpenSettings()
    {
        if (_window is null) return;
        var window = new SettingsWindow(Settings);
        window.Saved += () => _window.ApplySettings();
        window.Show();
        window.Activate();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path)) return new System.Drawing.Icon(path);
        }
        catch
        {
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void ToggleWindow()
    {
        if (_window is null) return;
        if (_window.IsVisible)
        {
            _window.Hide();
        }
        else
        {
            _window.Show();
            _window.Expand();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        MiniRuntime.Server.Stop();
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
