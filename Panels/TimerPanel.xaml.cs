using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kapsula.Services;

namespace Kapsula.Panels;

public partial class TimerPanel : UserControl
{
    public event Action? Back;

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };

    public TimerPanel()
    {
        InitializeComponent();
        _tick.Tick += (_, _) =>
        {
            TimerService.Update();
            Render();
        };
        _tick.Start();
        Render();
    }

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string minutes })
        {
            MinutesBox.Text = minutes;
            SecondsBox.Text = "0";
        }
    }

    private void OnMinutesWheel(object sender, MouseWheelEventArgs e)
    {
        Nudge(MinutesBox, e.Delta > 0 ? 5 : -5, 999);
        e.Handled = true;
    }

    private void OnSecondsWheel(object sender, MouseWheelEventArgs e)
    {
        Nudge(SecondsBox, e.Delta > 0 ? 5 : -5, 59);
        e.Handled = true;
    }

    private static void Nudge(TextBox box, int delta, int max)
    {
        int.TryParse(box.Text, out var value);
        box.Text = Math.Clamp(value + delta, 0, max).ToString();
        box.CaretIndex = box.Text.Length;
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        var span = ReadTime();
        if (span <= TimeSpan.Zero) span = TimeSpan.FromMinutes(5);
        TimerService.Start(span, Label(span));
        Render();
    }

    // В поле минут можно написать «2:30» — это минуты и секунды; поле секунд тогда не нужно.
    private TimeSpan ReadTime()
    {
        var text = MinutesBox.Text.Trim();
        if (text.Contains(':'))
        {
            var parts = text.Split(':');
            int.TryParse(parts[0], out var m);
            int.TryParse(parts.Length > 1 ? parts[1] : "0", out var s);
            return TimeSpan.FromSeconds(Math.Max(0, m) * 60 + Math.Clamp(s, 0, 59));
        }

        int.TryParse(text, out var minutes);
        int.TryParse(SecondsBox.Text.Trim(), out var seconds);
        return TimeSpan.FromSeconds(Math.Max(0, minutes) * 60 + Math.Clamp(seconds, 0, 59));
    }

    private static string Label(TimeSpan span)
    {
        var parts = new List<string>();
        if ((int)span.TotalMinutes > 0) parts.Add($"{(int)span.TotalMinutes} мин");
        if (span.Seconds > 0) parts.Add($"{span.Seconds} с");
        return parts.Count > 0 ? string.Join(" ", parts) : "таймер";
    }

    // Фокус-таймер: 25 минут работы, 5 минут перерыва; считаем циклы за сегодня.
    private void OnFocus(object sender, RoutedEventArgs e)
    {
        TimerService.Start(TimeSpan.FromMinutes(25), "фокус");

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (App.Settings.FocusDay != today)
        {
            App.Settings.FocusDay = today;
            App.Settings.FocusCycles = 0;
        }

        App.Settings.FocusCycles++;
        App.Settings.Save();
        Render();
    }

    private void OnBreak(object sender, RoutedEventArgs e)
    {
        TimerService.Start(TimeSpan.FromMinutes(5), "перерыв");
        Render();
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        TimerService.Stop();
        Render();
    }

    private void OnStopOne(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id }) TimerService.Stop(id);
        Render();
    }

    private void Render()
    {
        TimeText.Text = TimerService.IsRunning ? TimerService.Text : "00:00";

        StateText.Text = TimerService.Items.Count switch
        {
            0 => TimerService.IsFinished ? "время вышло" : "выбери время",
            1 => "идёт отсчёт",
            _ => $"идёт отсчётов: {TimerService.Items.Count}",
        };

        ActiveList.Children.Clear();
        var cycles = App.Settings.FocusDay == DateTime.Today.ToString("yyyy-MM-dd") ? App.Settings.FocusCycles : 0;
        CyclesText.Text = cycles == 0 ? "" : $"циклов фокуса сегодня: {cycles}";
        foreach (var entry in TimerService.Items.OrderBy(e => e.End))
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = entry.Label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Theme.TextBrush,
            };
            grid.Children.Add(label);

            var left = new TextBlock
            {
                Text = entry.Text,
                FontSize = 12,
                Margin = new Thickness(8, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Theme.DimBrush,
            };
            Grid.SetColumn(left, 1);
            grid.Children.Add(left);

            var stop = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Content = new TextBlock { Text = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10 },
                Tag = entry.Id,
                ToolTip = "Остановить этот",
            };
            stop.Click += OnStopOne;
            Grid.SetColumn(stop, 2);
            grid.Children.Add(stop);

            ActiveList.Children.Add(grid);
        }
    }

    // Панель закрывается — отсчёт продолжается, его показывает свёрнутая полоса.
    private void OnBack(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        Back?.Invoke();
    }
}
