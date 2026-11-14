using System.Windows;
using System.Windows.Controls;
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
        TimerService.Start(TimeSpan.FromMinutes(minutes));
        Render();
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        TimerService.Stop();
        Render();
    }

    private void Render()
    {
        TimeText.Text = TimerService.IsRunning || TimerService.IsFinished
            ? TimerService.Text
            : "00:00";

        StateText.Text = TimerService.IsRunning
            ? "идёт отсчёт"
            : TimerService.IsFinished ? "время вышло" : "выбери время";
    }

    // Панель закрывается — отсчёт продолжается, его показывает свёрнутая полоса.
    private void OnBack(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        Back?.Invoke();
    }
}
