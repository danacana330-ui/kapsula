using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class LauncherPanel : UserControl
{
    public event Action? Back;

    private readonly LauncherService _launcher = new();

    public LauncherPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _launcher.BuildIndex();
            var items = _launcher.Search("").ToList();
            Results.ItemsSource = items;
            if (items.Count > 0) Results.SelectedIndex = 0;
            Query.Focus();
        };
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e)
    {
        var items = _launcher.Search(Query.Text).ToList();
        Results.ItemsSource = items;
        if (items.Count > 0) Results.SelectedIndex = 0;
    }

    private void OnQueryKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Back?.Invoke();
                e.Handled = true;
                break;
            case Key.Down:
                if (Results.Items.Count > 0)
                    Results.SelectedIndex = Math.Min(Results.Items.Count - 1, Results.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up:
                if (Results.Items.Count > 0)
                    Results.SelectedIndex = Math.Max(0, Results.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Enter:
                LaunchSelected();
                e.Handled = true;
                break;
        }
    }

    private void OnSelect(object sender, SelectionChangedEventArgs e)
    {
    }

    private void OnClickResult(object sender, MouseButtonEventArgs e) => LaunchSelected();

    private void LaunchSelected()
    {
        if (Results.SelectedItem is not LaunchItem item) return;
        LauncherService.Run(item);
        Back?.Invoke();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
