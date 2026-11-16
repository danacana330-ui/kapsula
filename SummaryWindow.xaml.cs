using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Kapsula.Services;

namespace Kapsula;

/// <summary>Большая плашка сводки справа на экране: погода, планы, курсы, «я понял, спасибо».</summary>
public partial class SummaryWindow : Window
{
    private readonly AppSettings _settings;
    private readonly WeatherService _weather = new();
    private readonly CurrencyService _currency = new();

    public SummaryWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var today = DateTime.Today;
        DateText.Text = today.ToString("dddd, d MMMM", culture);

        Loaded += async (_, _) => await FillAsync();
    }

    public void ShowOnRight()
    {
        var work = SystemParameters.WorkArea;
        Show();
        UpdateLayout();

        Left = work.Right - ActualWidth - 18;
        Top = work.Top + Math.Max(18, (work.Height - ActualHeight) / 2);

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        Slide.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private async Task FillAsync()
    {
        FillPlans();
        await FillWeatherAsync();
        await FillRatesAsync();
    }

    private void FillPlans()
    {
        PlansPanel.Children.Clear();

        // напоминания на сегодня — отдельным блоком сверху
        foreach (var note in NotesStore.Today())
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var clock = new TextBlock
            {
                Text = note.RemindAt!.Value.ToString("HH:mm"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.AccentBrush,
                Margin = new Thickness(0, 0, 8, 0),
            };
            var text = new TextBlock
            {
                Text = note.Text,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.TextBrush,
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(clock);
            row.Children.Add(text);
            PlansPanel.Children.Add(row);
        }

        var plans = NotesStore.Notes.Where(n => !n.Done).Take(6).ToList();

        if (plans.Count == 0)
        {
            PlansPanel.Children.Add(new TextBlock
            {
                Text = "заметок-планов нет — день свободен",
                FontSize = 12.5,
                Foreground = Theme.DimBrush,
            });
            return;
        }

        foreach (var note in plans)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = Theme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 8, 0),
            };
            var text = new TextBlock
            {
                Text = note.Text,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.TextBrush,
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(dot);
            row.Children.Add(text);
            PlansPanel.Children.Add(row);
        }
    }

    private async Task FillWeatherAsync()
    {
        var now = await _weather.GetAsync(_settings.WeatherCity);
        if (now is null)
        {
            TempText.Text = "—";
            DescText.Text = "погода недоступна";
            return;
        }

        WeatherGlyph.Kind = now.Kind;
        TempText.Text = $"{now.Temp:+0;-0;0}°";
        DescText.Text = now.Description.Length > 0 ? now.Description : now.City;

        RangeText.Text = now.Days.Count > 0
            ? $"днём {now.Days[0].Max:+0;-0;0}°, ночью {now.Days[0].Min:+0;-0;0}°"
            : "";

        SunText.Text = now.Sunrise.Length > 0 && now.Sunset.Length > 0
            ? $"рассвет {To24h(now.Sunrise)} · закат {To24h(now.Sunset)}"
            : "";

        HintText.Text = Hint(now);
    }

    private async Task FillRatesAsync()
    {
        var rates = await _currency.GetAsync();
        RatesPanel.Children.Clear();

        if (rates.Count == 0)
        {
            RatesPanel.Children.Add(new TextBlock
            {
                Text = "курсы не загрузились — нет интернета",
                FontSize = 12.5,
                Foreground = Theme.DimBrush,
            });
            return;
        }

        foreach (var rate in rates)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = $"{rate.Sign}  {rate.Name}",
                FontSize = 12.5,
                Foreground = Theme.TextBrush,
            };
            var value = new TextBlock
            {
                Text = rate.Text + " ₽",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.TextBrush,
            };
            Grid.SetColumn(value, 1);
            row.Children.Add(name);
            row.Children.Add(value);
            RatesPanel.Children.Add(row);
        }
    }

    /// <summary>wttr.in отдаёт время как «08:46 AM» — переводим в 24 часа.</summary>
    private static string To24h(string text)
    {
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out var time))
            return time.ToString("HH:mm");
        return text;
    }

    private static string Hint(WeatherNow now) => now.Kind switch
    {
        "rain" or "thunder" => "Возьмите зонт.",
        "sleet" => "Мокрый снег — обувь получше и зонт.",
        "snow" => "Снег: выйдите заранее, дорога медленнее.",
        "fog" => "Утром туман — оставьте запас времени.",
        _ when now.Temp <= -10 => "Мороз: шапка и перчатки обязательны.",
        _ when now.Temp <= 0 => "Оденьтесь теплее.",
        _ when now.Temp >= 25 => "Жарко: пейте воду.",
        _ when now.Wind >= 25 => "Сильный ветер.",
        _ => "Спокойный день — хорошее время для дел.",
    };

    private void OnClose(object sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Close();
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
