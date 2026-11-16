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
    /// <summary>Закреплённое не вымывается из истории и всегда сверху.</summary>
    public bool Pinned { get; set; }
    public bool IsLink => Text.StartsWith("http", StringComparison.OrdinalIgnoreCase);

    [System.Text.Json.Serialization.JsonIgnore]
    public List<SmartAction> Actions => SmartText.Detect(Text);
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

    /// <summary>Похоже на пароль или ключ: без пробелов, есть буквы и цифры, 8–64 символа.</summary>
    public static bool LooksLikeSecret(string text)
    {
        if (text.Length is < 8 or > 64) return false;
        if (text.Any(char.IsWhiteSpace)) return false;
        var letters = text.Count(char.IsLetter);
        var digits = text.Count(char.IsDigit);
        var symbols = text.Length - letters - digits;
        return letters >= 3 && digits >= 2 && (symbols > 0 || (text.Any(char.IsUpper) && text.Any(char.IsLower)));
    }

    public static void Push(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        if (text.Length > 20000) text = text[..20000];
        if (App.Settings.SkipPasswords && LooksLikeSecret(text)) return;

        var existing = Items.FirstOrDefault(i => i.Text == text);
        if (existing is not null)
        {
            if (existing.Pinned) return;
            Items.Remove(existing);
        }

        // закреплённые держим сверху, новые кладём сразу после них
        var index = Items.Count(i => i.Pinned);
        Items.Insert(index, new ClipItem { Text = text, At = DateTime.Now });
        Trim();
        Store.Save("clipboard.json", Items.ToList());
    }

    private static void Trim()
    {
        while (Items.Count > Max)
        {
            var last = Items.LastOrDefault(i => !i.Pinned);
            if (last is null) return;
            Items.Remove(last);
        }
    }

    public static void TogglePin(ClipItem item)
    {
        item.Pinned = !item.Pinned;
        var index = Items.IndexOf(item);
        if (index >= 0) Items.Move(index, item.Pinned ? Items.Count(i => i.Pinned) - 1 : Items.Count(i => i.Pinned));
        Store.Save("clipboard.json", Items.ToList());
    }

    public static void Clear()
    {
        foreach (var item in Items.Where(i => !i.Pinned).ToList()) Items.Remove(item);
        Store.Save("clipboard.json", Items.ToList());
    }

    public static void Remove(ClipItem item)
    {
        Items.Remove(item);
        Store.Save("clipboard.json", Items.ToList());
    }

    /// <summary>Вычищает незакреплённые записи старше срока. Возвращает, сколько убрано.</summary>
    public static int PurgeOlderThan(TimeSpan age)
    {
        var cutoff = DateTime.Now - age;
        var stale = Items.Where(i => !i.Pinned && i.At < cutoff).ToList();
        foreach (var item in stale) Items.Remove(item);
        if (stale.Count > 0) Store.Save("clipboard.json", Items.ToList());
        return stale.Count;
    }

    /// <summary>Плановая чистка по сроку из настроек; 0 часов — не чистить.</summary>
    public static void Cleanup()
    {
        var hours = App.Settings.ClipboardCleanupHours;
        if (hours > 0) PurgeOlderThan(TimeSpan.FromHours(hours));
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
    /// <summary>Когда напомнить об этой заметке; null — напоминания нет.</summary>
    public DateTime? RemindAt { get; set; }
    /// <summary>О напоминании уже сообщили.</summary>
    public bool Reminded { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string RemindText => RemindAt is { } at
        ? "напомню " + (at.Date == DateTime.Today ? at.ToString("HH:mm") : at.ToString("dd.MM HH:mm"))
        : "";
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

    /// <summary>Ставит напоминание на «чч:мм»; если время уже прошло — на завтра.</summary>
    public static bool Remind(Note note, string time)
    {
        if (!TimeSpan.TryParse(time.Replace('.', ':'), out var at)) return false;
        var when = DateTime.Today + at;
        if (when <= DateTime.Now) when = when.AddDays(1);
        note.RemindAt = when;
        note.Reminded = false;
        Save();
        return true;
    }

    public static void ClearRemind(Note note)
    {
        note.RemindAt = null;
        note.Reminded = false;
        Save();
    }

    /// <summary>Заметки, о которых пора напомнить, — и сразу помечаем, чтобы не повторялись.</summary>
    public static List<Note> TakeDue()
    {
        var due = Notes.Where(n => n.RemindAt is { } at && !n.Reminded && at <= DateTime.Now).ToList();
        foreach (var note in due) note.Reminded = true;
        if (due.Count > 0) Save();
        return due;
    }

    public static IEnumerable<Note> Today()
    {
        var today = DateTime.Today;
        return Notes.Where(n => n.RemindAt is { } at && at.Date == today).OrderBy(n => n.RemindAt);
    }

    public static IEnumerable<Note> Latest(int count) =>
        Notes.OrderByDescending(n => n.At).Take(count);

    private static void Save() => Store.Save("notes.json", Notes.ToList());
}
