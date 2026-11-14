using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kapsula.Services;

/// <summary>
/// Поиск по картинке: загружает снимок в Google Lens, при сбое — в Яндекс.Картинки,
/// и отдаёт ссылку на готовую страницу результатов.
/// </summary>
public static class ReverseImageSearch
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        return client;
    }

    public static async Task<string?> FindAsync(byte[] png)
    {
        // Google Lens первым нельзя: его ссылка живёт в сессии загрузки и в браузере
        // открывается как «запрос для визуального поиска больше не действителен».
        return await YandexAsync(png) ?? await GoogleLensAsync(png);
    }

    private static async Task<string?> GoogleLensAsync(byte[] png)
    {
        try
        {
            var url = $"https://lens.google.com/v3/upload?hl=ru&stcs={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "encoded_image", "snimok.png");

            using var response = await Http.PostAsync(url, form);
            var location = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(location)) return null;

            if (location.StartsWith('/')) location = "https://lens.google.com" + location;
            return location;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> YandexAsync(byte[] png)
    {
        try
        {
            var request = JsonSerializer.Serialize(new
            {
                blocks = new[] { new { block = "b-page_type_search-by-image__link" } },
            });

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "upfile", "blob");

            using var response = await Http.PostAsync(
                $"https://yandex.ru/images/search?rpt=imageview&format=json&request={Uri.EscapeDataString(request)}",
                form);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var cbir = doc.RootElement
                .GetProperty("blocks")[0]
                .GetProperty("params")
                .GetProperty("cbirId")
                .GetString();

            return string.IsNullOrEmpty(cbir)
                ? null
                : $"https://yandex.ru/images/search?rpt=imageview&cbir_id={Uri.EscapeDataString(cbir)}";
        }
        catch (Exception)
        {
            return null;
        }
    }
}
