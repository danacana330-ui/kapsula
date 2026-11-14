using System.Net.Http;
using System.Text.Json;

namespace Kapsula.Services;

public sealed class WeatherNow
{
    public string City { get; init; } = "";
    public double Temp { get; init; }
    public double Feels { get; init; }
    public string Description { get; init; } = "";
    public string Kind { get; init; } = "cloud";
    public double Wind { get; init; }
    public List<(string Day, double Min, double Max, string Kind)> Days { get; } = new();
}

/// <summary>Погода через wttr.in — без ключей, доступен из России (open-meteo здесь блокируется).</summary>
public sealed class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<WeatherNow?> GetAsync(string city)
    {
        try
        {
            var url = "https://wttr.in/" + Uri.EscapeDataString(city) + "?format=j1&lang=ru";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var root = doc.RootElement;

            var current = root.GetProperty("current_condition")[0];
            var temp = Num(current, "temp_C");
            var feels = Num(current, "FeelsLikeC");
            var wind = Num(current, "windspeedKmph");
            var desc = "";
            if (current.TryGetProperty("lang_ru", out var ru) && ru.GetArrayLength() > 0)
                desc = ru[0].GetProperty("value").GetString() ?? "";
            if (desc.Length == 0 && current.TryGetProperty("weatherDesc", out var en) && en.GetArrayLength() > 0)
                desc = en[0].GetProperty("value").GetString() ?? "";

            var code = (int)Num(current, "weatherCode");
            var result = new WeatherNow
            {
                City = city,
                Temp = temp,
                Feels = feels,
                Wind = wind,
                Description = desc,
                Kind = KindFor(code),
            };

            if (root.TryGetProperty("weather", out var days))
            {
                var culture = new System.Globalization.CultureInfo("ru-RU");
                for (var i = 0; i < Math.Min(4, days.GetArrayLength()); i++)
                {
                    var day = days[i];
                    var date = DateTime.TryParse(day.GetProperty("date").GetString(), out var parsed)
                        ? parsed
                        : DateTime.Today;
                    var label = i == 0 ? "сегодня" : date.ToString("ddd", culture);
                    var dayCode = 113;
                    if (day.TryGetProperty("hourly", out var hourly) && hourly.GetArrayLength() > 0)
                        dayCode = (int)Num(hourly[0], "weatherCode");
                    result.Days.Add((label, Num(day, "mintempC"), Num(day, "maxtempC"), KindFor(dayCode)));
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static double Num(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    public static string Describe(int code) => code switch
    {
        113 => "ясно",
        116 => "переменная облачность",
        119 => "облачно",
        122 => "пасмурно",
        143 or 248 or 260 => "туман",
        >= 176 and <= 185 => "дождь",
        >= 200 and <= 232 => "гроза",
        >= 263 and <= 266 => "морось",
        >= 281 and <= 284 => "ледяной дождь",
        >= 293 and <= 308 => "дождь",
        >= 311 and <= 320 => "мокрый снег",
        >= 323 and <= 338 => "снег",
        350 => "ледяная крупа",
        >= 353 and <= 359 => "ливень",
        >= 362 and <= 377 => "снег с дождём",
        >= 386 and <= 395 => "гроза со снегом",
        _ => "облачно",
    };

    /// <summary>Код wttr.in → вид иконки (рисуется вектором в WeatherIcon).</summary>
    public static string KindFor(int code) => code switch
    {
        113 => "sun",
        116 => "partly",
        119 or 122 => "cloud",
        143 or 248 or 260 => "fog",
        >= 176 and <= 185 => "rain",
        >= 200 and <= 232 => "thunder",
        >= 263 and <= 308 => "rain",
        >= 311 and <= 320 => "sleet",
        >= 323 and <= 338 => "snow",
        350 => "sleet",
        >= 353 and <= 359 => "rain",
        >= 362 and <= 377 => "sleet",
        >= 386 and <= 395 => "thunder",
        _ => "cloud",
    };
}
