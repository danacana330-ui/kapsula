using System.Windows;
using System.Windows.Controls;
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
            MinutesBox.Text = minutes;
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinutesBox.Text, out var minutes) || minutes <= 0) minutes = 5;
        TimerService.Start(TimeSpan.FromMinutes(minutes), minutes + " мин");
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
