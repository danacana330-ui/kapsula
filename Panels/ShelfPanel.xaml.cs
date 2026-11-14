using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Kapsula.Services;
using Microsoft.Win32;

namespace Kapsula.Panels;

public sealed class ShelfEntry
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public string Info { get; init; } = "";
}

public partial class ShelfPanel : UserControl
{
    public event Action? Back;

    private readonly ObservableCollection<ShelfEntry> _view = new();

    public ShelfPanel()
    {
        InitializeComponent();
        List.ItemsSource = _view;
        Refresh();
    }

    public void Refresh()
    {
        _view.Clear();
        foreach (var path in ShelfStore.Files)
        {
            _view.Add(new ShelfEntry
            {
                Path = path,
                Name = Path.GetFileName(path),
                Info = Describe(path),
            });
        }
    }

    private static string Describe(string path)
    {
        try
        {
            if (Directory.Exists(path)) return "папка";
            var info = new FileInfo(path);
            if (!info.Exists) return "файл не найден";
            var size = info.Length switch
            {
                >= 1_073_741_824 => $"{info.Length / 1_073_741_824.0:0.0} ГБ",
                >= 1_048_576 => $"{info.Length / 1_048_576.0:0.0} МБ",
                >= 1024 => $"{info.Length / 1024.0:0} КБ",
                _ => $"{info.Length} Б",
            };
            return $"{size} · {info.LastWriteTime:dd.MM.yyyy HH:mm}";
        }
        catch
        {
            return "";
        }
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Что положить на полку" };
        if (dialog.ShowDialog() == true)
        {
            ShelfStore.Add(dialog.FileNames);
            Refresh();
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            ShelfStore.Add(paths);
            Refresh();
        }
    }

    private void OnOpen(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (List.SelectedItem is ShelfEntry entry)
        {
            try
            {
                Process.Start(new ProcessStartInfo(entry.Path) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ShelfEntry entry })
        {
            ShelfStore.Remove(entry.Path);
            Refresh();
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
