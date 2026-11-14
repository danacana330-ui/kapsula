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
        _ => "\uE8A5",
    };
}

/// <summary>Быстрый запуск: ярлыки меню «Пуск», файлы в личных папках, калькулятор, поиск в интернете.</summary>
public sealed class LauncherService
{
    private readonly List<LaunchItem> _apps = new();

    public void BuildIndex()
    {
        _apps.Clear();
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                    _apps.Add(new LaunchItem { Title = name, Path = lnk, Kind = "app" });
                }
            }
            catch
            {
            }
        }
    }

    public IEnumerable<LaunchItem> Search(string query, int limit = 8)
    {
        query = query.Trim();
        if (query.Length == 0) return Array.Empty<LaunchItem>();

        var results = new List<LaunchItem>();

        if (TryCalculate(query, out var calc))
            results.Add(new LaunchItem { Title = $"{query} = {calc}", Path = calc, Kind = "calc" });

        results.AddRange(_apps
            .Where(a => a.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
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
