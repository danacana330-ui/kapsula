using System.IO.Compression;

namespace Kapsula.Services;

/// <summary>
/// Копия всех данных капсулы одним архивом: настройки, заметки, буфер, история чата, таймеры.
/// Модели не входят — они большие и качаются заново.
/// </summary>
public static class BackupService
{
    public static string DataDir => Store.Dir;

    public static string DefaultPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return Path.Combine(desktop, $"Kapsula-копия-{DateTime.Now:yyyy-MM-dd-HHmm}.zip");
    }

    /// <summary>Упаковать данные. Возвращает путь архива или текст ошибки.</summary>
    public static (string? Path, string? Error) Export(string target)
    {
        try
        {
            var dir = DataDir;
            if (!Directory.Exists(dir)) return (null, "папки с данными нет");

            if (File.Exists(target)) File.Delete(target);
            ZipFile.CreateFromDirectory(dir, target, CompressionLevel.Optimal, false);
            return (target, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Развернуть архив в папку данных с заменой файлов.</summary>
    public static string? Import(string archive)
    {
        try
        {
            var dir = DataDir;
            Directory.CreateDirectory(dir);
            ZipFile.ExtractToDirectory(archive, dir, true);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
