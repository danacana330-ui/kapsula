using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Kapsula.Services;

/// <summary>Хранилище простых списков (буфер, полка, заметки) в %APPDATA%\Kapsula.</summary>
public static class Store
{
    public static string Dir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kapsula");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static T Load<T>(string name, T fallback)
    {
        try
        {
            var path = Path.Combine(Dir, name);
            if (!File.Exists(path)) return fallback;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static void Save<T>(string name, T value)
    {
        try
        {
            File.WriteAllText(Path.Combine(Dir, name), JsonSerializer.Serialize(value));
        }
        catch
        {
        }
    }
}

public sealed class ClipItem
{
    public string Text { get; set; } = "";
    public DateTime At { get; set; }
    public bool IsLink => Text.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}

public static class ClipboardStore
{
    private const int Max = 60;
    public static ObservableCollection<ClipItem> Items { get; } = new();

    public static void Load()
    {
        foreach (var item in Store.Load("clipboard.json", new List<ClipItem>()))
            Items.Add(item);
    }

    public static void Push(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        if (text.Length > 20000) text = text[..20000];
        var existing = Items.FirstOrDefault(i => i.Text == text);
        if (existing is not null) Items.Remove(existing);
        Items.Insert(0, new ClipItem { Text = text, At = DateTime.Now });
        while (Items.Count > Max) Items.RemoveAt(Items.Count - 1);
        Store.Save("clipboard.json", Items.ToList());
    }

    public static void Clear()
    {
        Items.Clear();
        Store.Save("clipboard.json", Items.ToList());
    }

    public static void Remove(ClipItem item)
    {
        Items.Remove(item);
        Store.Save("clipboard.json", Items.ToList());
    }
}

public static class ShelfStore
{
    public static ObservableCollection<string> Files { get; } = new();

    public static void Load()
    {
        foreach (var f in Store.Load("shelf.json", new List<string>()))
            if (File.Exists(f) || Directory.Exists(f))
                Files.Add(f);
    }

    public static void Add(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (Files.Contains(p)) continue;
            Files.Insert(0, p);
        }
        while (Files.Count > 40) Files.RemoveAt(Files.Count - 1);
        Save();
    }

    public static void Remove(string path)
    {
        Files.Remove(path);
        Save();
    }

    private static void Save() => Store.Save("shelf.json", Files.ToList());
}

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public string Folder { get; set; } = "Общее";
    public DateTime At { get; set; } = DateTime.Now;
    public bool Done { get; set; }
}

public static class NotesStore
{
    public const string DefaultFolder = "Общее";

    public static ObservableCollection<Note> Notes { get; } = new();

    public static ObservableCollection<string> Folders { get; } = new();

    public static void Load()
    {
        var folders = Store.Load("note_folders.json", new List<string> { DefaultFolder, "Работа", "Идеи" });
        foreach (var f in folders)
            if (!string.IsNullOrWhiteSpace(f) && !Folders.Contains(f))
                Folders.Add(f);
        if (!Folders.Contains(DefaultFolder)) Folders.Insert(0, DefaultFolder);

        foreach (var n in Store.Load("notes.json", new List<Note>()))
        {
            if (string.IsNullOrWhiteSpace(n.Folder)) n.Folder = DefaultFolder;
            if (!Folders.Contains(n.Folder)) Folders.Add(n.Folder);
            Notes.Add(n);
        }
    }

    public static Note Add(string text, string folder = DefaultFolder)
    {
        var n = new Note { Text = text, Folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder };
        Notes.Insert(0, n);
        Save();
        return n;
    }

    public static void AddFolder(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || Folders.Contains(name)) return;
        Folders.Add(name);
        Store.Save("note_folders.json", Folders.ToList());
    }

    public static void Update(Note note)
    {
        Save();
    }

    public static void Remove(Note note)
    {
        Notes.Remove(note);
        Save();
    }

    private static void Save() => Store.Save("notes.json", Notes.ToList());
}
