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
    public bool ShowLiquidEdge { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    public string UiFont { get; set; } = "Segoe UI Variable Display";
    public bool Detached { get; set; }
    public double WindowLeft { get; set; } = -1;   // -1 — позиция не задана, капсула по центру
    public double WindowTop { get; set; } = -1;
    public string ClockFormat { get; set; } = "HH:mm";
    public string AiModel { get; set; } = "qwen3:4b";
    public string AiEndpoint { get; set; } = "http://localhost:11434";
    public string WeatherCity { get; set; } = "Москва";
    public int VolumeStep { get; set; } = 2;
    public bool ShowOsd { get; set; } = true;
    public string RecordFolder { get; set; } = "";
    public List<string> HomeTiles { get; set; } = new()
    {
        "clipboard", "shelf", "notes", "ai", "shot", "weather", "quick", "timer", "record",
    };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kapsula");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // повреждённый файл настроек не должен мешать запуску
        }
        return new AppSettings();
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
