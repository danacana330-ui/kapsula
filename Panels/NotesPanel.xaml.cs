using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class NotesPanel : UserControl
{
    public event Action? Back;

    private readonly ObservableCollection<Note> _view = new();
    private string _folder = NotesStore.DefaultFolder;

    public NotesPanel()
    {
        InitializeComponent();
        List.ItemsSource = _view;
        Refresh();
        Loaded += (_, _) => Input.Focus();
    }

    private void Refresh()
    {
        _view.Clear();
        foreach (var note in NotesStore.Notes)
            if (note.Folder == _folder)
                _view.Add(note);

        Counter.Text = _view.Count == 0 ? "" : $"{_view.Count} шт.";
        BuildFolders();
    }

    private void BuildFolders()
    {
        FolderBar.Children.Clear();
        foreach (var folder in NotesStore.Folders)
        {
            var active = folder == _folder;
            var button = new Button
            {
                Content = folder,
                Tag = folder,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Foreground = active ? Theme.AccentBrush : Theme.DimBrush,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                Background = active ? Theme.CardHoverBrush : Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            button.Click += OnPickFolder;
            FolderBar.Children.Add(button);
        }

        var add = new Button
        {
            Content = "+ папка",
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(10, 4, 10, 4),
            Cursor = Cursors.Hand,
            Foreground = Theme.DimBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        add.Click += (_, _) =>
        {
            NewFolderRow.Visibility = Visibility.Visible;
            FolderInput.Focus();
        };
        FolderBar.Children.Add(add);
    }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string folder }) return;
        _folder = folder;
        Refresh();
    }

    private void OnFolderKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CreateFolder();
    }

    private void OnCreateFolder(object sender, RoutedEventArgs e) => CreateFolder();

    private void CreateFolder()
    {
        var name = FolderInput.Text.Trim();
        if (name.Length == 0) return;
        NotesStore.AddFolder(name);
        _folder = name;
        FolderInput.Text = "";
        NewFolderRow.Visibility = Visibility.Collapsed;
        Refresh();
        Input.Focus();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => AddButtonState();

    private void AddButtonState()
    {
    }

    private void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Add();
            e.Handled = true;
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e) => Add();

    private void Add()
    {
        var text = Input.Text.Trim();
        if (text.Length == 0) return;
        NotesStore.Add(text, _folder);
        Input.Text = "";
        Refresh();
        Input.Focus();
    }

    private void OnToggleDone(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
            NotesStore.Update(note);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Note note })
        {
            NotesStore.Remove(note);
            Refresh();
        }
    }

    private void OnSetRemind(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Note note)
        {
            RemindBox.ToolTip = "Сначала выбери заметку в списке";
            return;
        }

        if (NotesStore.Remind(note, RemindBox.Text.Trim())) Refresh();
    }

    private void OnClearRemind(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Note note) return;
        NotesStore.ClearRemind(note);
        Refresh();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
