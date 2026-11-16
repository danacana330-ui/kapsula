using System.Diagnostics;
using System.IO;

namespace Kapsula.Services;

public sealed class LaunchItem
{
    public string Title { get; init; } = "";
    public string Path { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Glyph => Kind switch
    {
        "app" => "\uE71D",
        "file" => "\uE8A5",
        "folder" => "\uE8B7",
        "calc" => "\uE8EF",
        "web" => "\uE774",
        "panel" => "\uE8B9",
        "settings" => "\uE713",
        "clip" => "\uE77F",
        "note" => "\uE70B",
        "shelf" => "\uE8B7",
        _ => "\uE8A5",
    };
}

/// <summary>Быстрый запуск: ярлыки меню «Пуск», файлы в личных папках, калькулятор, поиск в интернете.</summary>
public sealed class LauncherService
{
    private const string UsageFile = "launch_usage.json";

    /// <summary>Разделы самой капсулы — их тоже ищем из общего поля.</summary>
    private static readonly (string Title, string Key, string Words)[] Builtin =
    {
        ("Буфер обмена", "clipboard", "буфер обмена clipboard копии"),
        ("Заметки", "notes", "заметки notes записи"),
        ("Таймер", "timer", "таймер timer"),
        ("Погода", "weather", "погода weather"),
        ("ИИ-помощник", "ai", "ии помощник чат нейросеть ai"),
        ("Скриншот", "shot", "скриншот снимок screenshot"),
        ("Быстрые действия", "quick", "быстрые действия переключатели quick"),
        ("Полка файлов", "shelf", "полка файлы shelf"),
        ("Сводка дня", "summary", "сводка дня summary"),
        ("Вид и поведение", "appearance", "вид внешний оформление тема appearance"),
        ("Компьютер", "system", "система процессор память батарея компьютер system"),
        ("Настройки Kapsula", "settings", "настройки settings параметры"),
    };

    private static Dictionary<string, int> _usage = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<LaunchItem> _apps = new();

    public void BuildIndex()
    {
        _apps.Clear();
        _usage = Store.Load(UsageFile, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        foreach (var root in roots.Where(Directory.Exists))
            AddShortcuts(root);
    }

    private static int CountOf(string path) => _usage.TryGetValue(path, out var n) ? n : 0;

    private static void Remember(string path)
    {
        _usage[path] = CountOf(path) + 1;
        Store.Save(UsageFile, _usage);
    }

    /// <summary>
    /// Обход дерева «Пуска» по одной папке: в меню бывают закрытые для чтения каталоги
    /// (например, «Программы»), из-за которых Directory.EnumerateFiles(AllDirectories) падает
    /// целиком и список приложений остаётся пустым.
    /// </summary>
    private void AddShortcuts(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.lnk");
            }
            catch
            {
                continue;
            }

            foreach (var lnk in files)
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (!IsAppName(name)) continue;
                if (_apps.Any(a => a.Title.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                _apps.Add(new LaunchItem { Title = name, Path = lnk, Kind = "app" });
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var sub in subdirs) pending.Push(sub);
        }
    }

    /// <summary>Служебные ярлыки (удаление, справка, документация) в списке запуска не нужны.</summary>
    private static bool IsAppName(string name)
    {
        string[] skip =
        {
            "uninstall", "деинсталл", "удалить", "удаление", "help", "справка",
            "документац", "руководств", "readme", "changelog", "лицензи",
            "запустить", "журнал телеметрии",
        };
        return !skip.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<LaunchItem> Search(string query, int limit = 8)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            // пустой запрос — то, чем пользуются чаще, сверху
            return _apps
                .OrderByDescending(a => CountOf(a.Path))
                .ThenBy(a => a.Title.Length)
                .ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(limit);
        }

        var results = new List<LaunchItem>();

        if (TryCalculate(query, out var calc))
            results.Add(new LaunchItem { Title = $"{query} = {calc}", Path = calc, Kind = "calc" });

        results.AddRange(Builtin
            .Where(b => b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        b.Words.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(b => new LaunchItem
            {
                Title = b.Title,
                Path = b.Key,
                Kind = b.Key == "settings" ? "settings" : "panel",
            }));

        results.AddRange(SearchOwn(query, 3));

        results.AddRange(_apps
            .Where(a => a.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => CountOf(a.Path))
            .ThenBy(a => a.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(a => a.Title.Length)
            .Take(limit));

        if (results.Count < limit)
            results.AddRange(SearchFiles(query, limit - results.Count));

        if (results.Count < limit)
            results.Add(new LaunchItem
            {
                Title = $"Найти «{query}» в интернете",
                Path = "https://www.google.com/search?q=" + Uri.EscapeDataString(query),
                Kind = "web",
            });

        return results;
    }

    /// <summary>Поиск по своим данным: буфер, заметки, полка.</summary>
    private static IEnumerable<LaunchItem> SearchOwn(string query, int limit)
    {
        var found = new List<LaunchItem>();

        foreach (var clip in ClipboardStore.Items
                     .Where(i => i.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Take(limit))
        {
            found.Add(new LaunchItem
            {
                Title = "Буфер: " + One(clip.Text),
                Path = clip.Text,
                Kind = "clip",
            });
            if (found.Count >= limit) return found;
        }

        foreach (var note in NotesStore.Notes
                     .Where(n => n.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Take(limit))
        {
            found.Add(new LaunchItem
            {
                Title = "Заметка: " + One(note.Text),
                Path = note.Id,
                Kind = "note",
            });
            if (found.Count >= limit) return found;
        }

        foreach (var file in ShelfStore.Files
                     .Where(f => Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Take(limit))
        {
            found.Add(new LaunchItem
            {
                Title = "Полка: " + Path.GetFileName(file),
                Path = file,
                Kind = "shelf",
            });
            if (found.Count >= limit) return found;
        }

        return found;
    }

    private static string One(string text)
    {
        var line = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return line.Length > 40 ? line[..40] + "…" : line;
    }

    private static IEnumerable<LaunchItem> SearchFiles(string query, int limit)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        };

        var found = new List<LaunchItem>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(root, "*" + query + "*", SearchOption.TopDirectoryOnly))
                {
                    var isDir = Directory.Exists(path);
                    found.Add(new LaunchItem
                    {
                        Title = Path.GetFileName(path),
                        Path = path,
                        Kind = isDir ? "folder" : "file",
                    });
                    if (found.Count >= limit) return found;
                }
            }
            catch
            {
            }
        }
        return found;
    }

    public static void Run(LaunchItem item)
    {
        try
        {
            if (item.Kind == "calc")
            {
                System.Windows.Clipboard.SetText(item.Path);
                return;
            }

            if (item.Kind == "panel")
            {
                MainWindow.Instance?.OpenTile(item.Path);
                return;
            }

            if (item.Kind == "settings")
            {
                new SettingsWindow(App.Settings).Show();
                return;
            }

            if (item.Kind == "clip")
            {
                System.Windows.Clipboard.SetText(item.Path);
                return;
            }

            if (item.Kind == "note")
            {
                MainWindow.Instance?.OpenTile("notes");
                return;
            }

            Remember(item.Path);
            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public static bool TryCalculate(string expr, out string result)
    {
        result = "";
        if (expr.Length == 0) return false;
        if (!expr.Any(char.IsDigit)) return false;
        if (!expr.All(c => char.IsDigit(c) || "+-*/().,%^ ".Contains(c))) return false;
        try
        {
            var value = new Parser(expr).Parse();
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            result = Math.Abs(value - Math.Round(value)) < 1e-9
                ? ((long)Math.Round(value)).ToString()
                : value.ToString("0.####");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s) => _s = s.Replace(',', '.').Replace(" ", "");

        public double Parse()
        {
            var v = Expr();
            if (_i < _s.Length) throw new FormatException();
            return v;
        }

        private double Expr()
        {
            var v = Term();
            while (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-'))
            {
                var op = _s[_i++];
                var r = Term();
                v = op == '+' ? v + r : v - r;
            }
            return v;
        }

        private double Term()
        {
            var v = Power();
            while (_i < _s.Length && (_s[_i] == '*' || _s[_i] == '/'))
            {
                var op = _s[_i++];
                var r = Power();
                v = op == '*' ? v * r : v / r;
            }
            return v;
        }

        private double Power()
        {
            var v = Unary();
            if (_i < _s.Length && _s[_i] == '^')
            {
                _i++;
                return Math.Pow(v, Power());
            }
            return v;
        }

        private double Unary()
        {
            if (_i < _s.Length && _s[_i] == '-') { _i++; return -Unary(); }
            if (_i < _s.Length && _s[_i] == '+') { _i++; return Unary(); }
            return Atom();
        }

        private double Atom()
        {
            if (_i < _s.Length && _s[_i] == '(')
            {
                _i++;
                var v = Expr();
                if (_i >= _s.Length || _s[_i] != ')') throw new FormatException();
                _i++;
                return v;
            }
            var start = _i;
            while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
            if (start == _i) throw new FormatException();
            return double.Parse(_s[start.._i], System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
