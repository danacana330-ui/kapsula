using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class ClipboardPanel : UserControl
{
    public event Action? Back;

    private readonly ObservableCollection<ClipItem> _view = new();

    public ClipboardPanel()
    {
        InitializeComponent();
        List.ItemsSource = _view;
        Filter("");
        Loaded += (_, _) => Search.Focus();
    }

    private void Filter(string query)
    {
        _view.Clear();
        foreach (var item in ClipboardStore.Items)
        {
            if (query.Length == 0 || item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                _view.Add(item);
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Filter(Search.Text);

    private void OnPick(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not ClipItem item) return;
        try
        {
            Clipboard.SetText(item.Text);
        }
        catch
        {
        }
        List.SelectedItem = null;
        Back?.Invoke();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ClipItem item })
        {
            ClipboardStore.Remove(item);
            Filter(Search.Text);
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        ClipboardStore.Clear();
        Filter(Search.Text);
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
