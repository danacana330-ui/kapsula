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
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private Note? _remindTarget;
    private DateTime _remindDay = DateTime.Today;

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

    // ── напоминание ──────────────────────────────────────────────────────────

    private void OnAddRemind(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is not Note note)
        {
            RemindHint.Text = "сначала выбери заметку в списке";
            return;
        }

        _remindTarget = note;
        _remindDay = note.RemindAt?.Date ?? DateTime.Today;
        _month = new DateTime(_remindDay.Year, _remindDay.Month, 1);
        RemindHint.Text = "выбери день";
        CalendarHost.Visibility = Visibility.Visible;
        TimeRow.Visibility = Visibility.Collapsed;
        BuildCalendar();
    }

    private void OnCancelRemind(object sender, RoutedEventArgs e)
    {
        CalendarHost.Visibility = Visibility.Collapsed;
        TimeRow.Visibility = Visibility.Collapsed;
        _remindTarget = null;
        RemindHint.Text = "Добавить напоминание";
    }

    private void OnSetRemind(object sender, RoutedEventArgs e) => ApplyRemind();

    private void OnRemindKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyRemind();
            e.Handled = true;
        }
    }

    private void ApplyRemind()
    {
        var note = _remindTarget ?? List.SelectedItem as Note;
        if (note is null)
        {
            RemindHint.Text = "сначала выбери заметку в списке";
            return;
        }

        var text = RemindBox.Text.Trim().Replace('.', ':');
        if (!TimeSpan.TryParse(text, out var at))
        {
            TimeHint.Text = "время вида 18:30";
            return;
        }

        var when = _remindDay.Date + at;
        if (when <= DateTime.Now)
        {
            TimeHint.Text = "это время уже прошло";
            return;
        }

        NotesStore.Remind(note, when);
        OnCancelRemind(this, new RoutedEventArgs());
        Refresh();
    }

    private void OnClearRemind(object sender, RoutedEventArgs e)
    {
        var note = _remindTarget ?? List.SelectedItem as Note;
        if (note is null) return;
        NotesStore.ClearRemind(note);
        OnCancelRemind(this, new RoutedEventArgs());
        Refresh();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();

    // ── календарь ────────────────────────────────────────────────────────────

    private void OnPrevMonth(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        BuildCalendar();
    }

    private void OnNextMonth(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        BuildCalendar();
    }

    private void BuildCalendar()
    {
        MonthTitle.Text = _month.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));

        CalendarGrid.Children.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.RowDefinitions.Clear();
        for (var c = 0; c < 7; c++)
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var r = 0; r < 7; r++)
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var names = new[] { "пн", "вт", "ср", "чт", "пт", "сб", "вс" };
        for (var c = 0; c < 7; c++)
            Put(new TextBlock
            {
                Text = names[c],
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Theme.DimBrush,
                Margin = new Thickness(0, 0, 0, 4),
            }, 0, c);

        var offset = ((int)_month.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(_month.Year, _month.Month);
        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(_month.Year, _month.Month, day);
            var index = offset + day - 1;
            var marked = NotesStore.Notes.Any(n => n.RemindAt?.Date == date);
            var selected = _remindDay.Date == date;

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new TextBlock
            {
                Text = day.ToString(),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = selected ? Theme.AccentBrush : Theme.TextBrush,
                FontWeight = marked ? FontWeights.SemiBold : FontWeights.Normal,
            });
            if (marked)
                content.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Margin = new Thickness(0, 1, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Fill = Theme.AccentBrush,
                });

            var button = new Button
            {
                Content = content,
                Padding = new Thickness(2, 3, 2, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Background = selected ? Theme.CardHoverBrush : Brushes.Transparent,
            };
            button.Click += (_, _) => PickDay(date);

            Put(button, index / 7 + 1, index % 7);
        }
    }

    private void Put(UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        CalendarGrid.Children.Add(element);
    }

    private void PickDay(DateTime date)
    {
        _remindDay = date;
        CalendarHost.Visibility = Visibility.Collapsed;
        TimeRow.Visibility = Visibility.Visible;
        TimeHint.Text = $"{date:dd.MM}, во сколько?";
        if (string.IsNullOrWhiteSpace(RemindBox.Text)) RemindBox.Text = "18:00";
        RemindBox.Focus();
        RemindBox.SelectAll();
    }
}
