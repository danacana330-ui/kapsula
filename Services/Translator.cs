namespace Kapsula.Services;

/// <summary>Перевод выделенного текста: свой сервер, а если он не настроен — бесплатное облако.</summary>
public static class Translator
{
    public static async Task<string?> ToRussianAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var messages = new List<AiMessage>
        {
            new()
            {
                Role = "user",
                Content = "Переведи на русский язык. Ответь только переводом, без пояснений:\n" + text,
            },
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(App.Settings.AiServerUrl))
            {
                var answer = await CustomServer.ChatAsync(
                    App.Settings.AiServerUrl, App.Settings.AiServerKey, App.Settings.AiServerModel,
                    messages, _ => { }, CancellationToken.None);
                return string.IsNullOrWhiteSpace(answer) ? null : answer.Trim();
            }

            var cloud = await FreeCloud.ChatAsync(
                App.Settings.AiGatewayPort, "free-auto", messages, _ => { }, CancellationToken.None);
            return string.IsNullOrWhiteSpace(cloud) ? null : cloud.Trim();
        }
        catch
        {
            return null;
        }
    }
}
