using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kapsula.Services;

namespace Kapsula.Panels;

public sealed class DayRow
{
    public string Day { get; init; } = "";
    public string Kind { get; init; } = "cloud";
    public string Range { get; init; } = "";
}

public partial class WeatherPanel : UserControl
{
    public event Action? Back;

    private readonly WeatherService _service = new();

    public WeatherPanel()
    {
        InitializeComponent();
        CityBox.Text = AppSettings.Load().WeatherCity;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        StatusText.Text = "Загружаю…";
        var city = CityBox.Text.Trim();
        if (city.Length == 0) city = "Москва";

        var w = await _service.GetAsync(city);
        if (w is null)
        {
            StatusText.Text = "Не удалось получить погоду. Проверь название города или интернет.";
            return;
        }

        BigIcon.Kind = w.Kind;
        TempText.Text = $"{Math.Round(w.Temp)}°";
        DescText.Text = w.Description;
        FeelsText.Text = $"ощущается как {Math.Round(w.Feels)}° · ветер {Math.Round(w.Wind)} м/с";
        StatusText.Text = w.City;

        DaysList.ItemsSource = w.Days
            .Select(d => new DayRow { Day = d.Day, Kind = d.Kind, Range = $"{Math.Round(d.Min)}° … {Math.Round(d.Max)}°" })
            .ToList();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void OnCityKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var settings = AppSettings.Load();
        settings.WeatherCity = CityBox.Text.Trim();
        settings.Save();
        await LoadAsync();
    }

    private void OnBack(object sender, RoutedEventArgs e) => Back?.Invoke();
}
