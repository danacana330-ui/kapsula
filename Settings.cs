using System.IO;
using System.Text.Json;

namespace Kapsula;

public sealed class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool ShowSeconds { get; set; }
    public bool ExpandOnHover { get; set; } = true;
    public bool LightTheme { get; set; }
    public bool LightRim { get; set; }
    public bool ShowShadow { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public bool FollowCursor { get; set; } = true;
    public bool ShowPrivacyDots { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool CopyToast { get; set; } = true;
    /// <summary>Знакомство при первом запуске уже показано.</summary>
    public bool Onboarded { get; set; }
    /// <summary>Не запоминать в истории буфера строки, похожие на пароли и ключи.</summary>
    public bool SkipPasswords { get; set; } = true;
    /// <summary>Через сколько часов вычищать историю буфера; 0 — не чистить.</summary>
    public int ClipboardCleanupHours { get; set; } = 72;
    /// <summary>Сколько циклов фокуса сделано и за какой день считали.</summary>
    public int FocusCycles { get; set; }
    public string FocusDay { get; set; } = "";
    public bool ShowLiquidEdge { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    public string UiFont { get; set; } = "Segoe UI Variable Display";
    public bool Detached { get; set; }
    public double WindowLeft { get; set; } = -1;   // -1 — позиция не задана, капсула по центру
    public double WindowTop { get; set; } = -1;
    public string ClockFormat { get; set; } = "HH:mm";
    public string AiModel { get; set; } = "qwen3:4b";
    public string AiEndpoint { get; set; } = "http://localhost:11434";
    /// <summary>Порт шлюза бесплатных облачных моделей (Desktop\FreeLLM\gateway.py).</summary>
    public int AiGatewayPort { get; set; } = 8090;
    /// <summary>Свой сервер: адрес вида https://host/v1, ключ и имя модели (любой OpenAI-совместимый).</summary>
    public string AiServerUrl { get; set; } = "";
    public string AiServerKey { get; set; } = "";
    public string AiServerModel { get; set; } = "";
    public string WeatherCity { get; set; } = "Москва";
    public int VolumeStep { get; set; } = 2;
    public bool ShowOsd { get; set; } = true;
    public bool RecordSystemAudio { get; set; } = true;
    public bool SummaryEnabled { get; set; } = true;
    public string SummaryTime { get; set; } = "09:00";
    public string SummaryShownOn { get; set; } = "";
    public string RecordFolder { get; set; } = "";
    public List<string> HomeTiles { get; set; } = new()
    {
        "clipboard", "shelf", "notes", "ai", "shot", "weather", "quick", "timer", "record", "summary", "appearance",
    };

    /// <summary>Версия формата настроек: по ней досыпаем новые пункты в старые файлы.</summary>
    public int Version { get; set; }

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kapsula");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch
        {
            // повреждённый файл настроек не должен мешать запуску
            settings = new AppSettings();
        }

        if (settings.Version < 2)
        {
            // 2.0: у старых файлов нет плитки «Сводка» — досыпаем её один раз
            settings.HomeTiles ??= new List<string>();
            if (!settings.HomeTiles.Contains("summary")) settings.HomeTiles.Add("summary");
            settings.Version = 2;
            settings.Save();
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}
