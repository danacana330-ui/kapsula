using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        // панель открыта, а в буфер что-то скопировали — показываем сразу
        ClipboardStore.Items.CollectionChanged += OnItemsChanged;
        Unloaded += (_, _) => ClipboardStore.Items.CollectionChanged -= OnItemsChanged;
        Loaded += (_, _) => Search.Focus();
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Filter(Search.Text);

    private void Filter(string query)
    {
        _view.Clear();
        // закреплённые сверху, дальше по свежести
        foreach (var item in ClipboardStore.Items.OrderByDescending(i => i.Pinned))
        {
            if (query.Length == 0 || item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                _view.Add(item);
        }
    }

    private void OnPin(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ClipItem item }) return;
        ClipboardStore.TogglePin(item);
        Filter(Search.Text);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Filter(Search.Text);

    /// <summary>Кнопка действия под строкой — не выбирает строку, а сразу делает дело.</summary>
    private void OnSmart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SmartAction action }) return;
        e.Handled = true;
        SmartText.Perform(action);
    }

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
