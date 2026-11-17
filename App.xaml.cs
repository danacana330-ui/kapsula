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

    /// <summary>Журнал ошибок рядом с настройками — его можно открыть из трея.</summary>
    public static string LogPath => Path.Combine(BackupService.DataDir, "errors.log");

    public static void LogError(Exception exception)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n---\n");
        }
        catch
        {
        }
    }

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
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception) LogError(exception);
        };

        _mutex = new Mutex(true, "Kapsula.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Settings = AppSettings.Load();
        Autostart.Apply(Settings.StartWithWindows);
        Theme.RegisterResources(Resources);

        try
        {
            _window = new MainWindow();
            _window.SourceInitialized += (_, _) => WindowHelper.MakeOverlay(_window);
            _window.Show();
        }
        catch (Exception exception)
        {
            LogError(exception);
            throw;
        }

        if (!Settings.Onboarded)
        {
            var onboarding = new OnboardingWindow();
            onboarding.Show();
            onboarding.Activate();
        }

        CreateTray();

        DispatcherUnhandledException += (_, args) =>
        {
            // оверлей не должен падать от единичного сбоя системного API
            LogError(args.Exception);
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
        menu.Items.Add("Копия данных…", null, (_, _) => ExportData());
        menu.Items.Add("Восстановить из копии…", null, (_, _) => ImportData());
        menu.Items.Add("Журнал ошибок", null, (_, _) => OpenLog());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ToggleWindow();
    }

    public void OpenSettings()
    {
        _window?.ShowSettings();
    }

    /// <summary>Сложить настройки, заметки, буфер и историю чата в один архив.</summary>
    private void ExportData()
    {
        var dialog = new WinForms.SaveFileDialog
        {
            Title = "Сохранить копию данных Kapsula",
            Filter = "Архив данных (*.zip)|*.zip",
            FileName = Path.GetFileName(BackupService.DefaultPath()),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        var (path, error) = BackupService.Export(dialog.FileName);
        Notify("Kapsula", error is null ? "Копия сохранена: " + path : "Не удалось сохранить: " + error);
    }

    /// <summary>Развернуть архив обратно и предложить перезапуск.</summary>
    private void ImportData()
    {
        var dialog = new WinForms.OpenFileDialog
        {
            Title = "Восстановить данные Kapsula",
            Filter = "Архив данных (*.zip)|*.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        var answer = MessageBox.Show(
            "Заменить текущие заметки, буфер и настройки данными из копии?",
            "Kapsula", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var error = BackupService.Import(dialog.FileName);
        if (error is not null)
        {
            Notify("Kapsula", "Не удалось восстановить: " + error);
            return;
        }

        var restart = MessageBox.Show("Данные восстановлены. Перезапустить капсулу сейчас?",
            "Kapsula", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (restart != MessageBoxResult.Yes) return;

        try
        {
            var exe = Environment.ProcessPath;
            if (exe is not null) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch
        {
        }
        Shutdown();
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(LogPath))
            {
                Notify("Kapsula", "Журнал ошибок пуст");
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogPath) { UseShellExecute = true });
        }
        catch
        {
        }
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
        Services.ScreenBrightness.Restore();
        MiniRuntime.Server.Stop();
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
