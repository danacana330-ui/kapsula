using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Xml;

namespace Kapsula.Services;

public sealed class CurrencyRate
{
    public string Code { get; init; } = "";
    public string Sign { get; init; } = "";
    public string Name { get; init; } = "";
    public double Value { get; init; }
    public string Text => Value > 0 ? Value.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU")) : "—";
}

/// <summary>Официальные курсы ЦБ РФ (юань, доллар, евро) из XML ЦБ.</summary>
public sealed class CurrencyService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly (string Code, string Sign)[] Wanted =
    {
        ("CNY", "¥"), ("USD", "$"), ("EUR", "€"),
    };

    static CurrencyService()
    {
        // XML ЦБ в windows-1251, без провайдера кодировок его не прочитать
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<List<CurrencyRate>> GetAsync()
    {
        var result = new List<CurrencyRate>();
        try
        {
            var bytes = await Http.GetByteArrayAsync("https://www.cbr.ru/scripts/XML_daily.asp");
            var text = Encoding.GetEncoding(1251).GetString(bytes);

            var doc = new XmlDocument();
            doc.LoadXml(text);

            foreach (var (code, sign) in Wanted)
            {
                var node = doc.SelectSingleNode($"/ValCurs/Valute[CharCode='{code}']");
                if (node is null) continue;

                var nominal = Parse(node.SelectSingleNode("Nominal")?.InnerText);
                var value = Parse(node.SelectSingleNode("Value")?.InnerText);
                if (nominal <= 0) nominal = 1;

                result.Add(new CurrencyRate
                {
                    Code = code,
                    Sign = sign,
                    Name = node.SelectSingleNode("Name")?.InnerText ?? code,
                    Value = value / nominal,
                });
            }
        }
        catch
        {
            // без интернета сводка показывается с прочерками
        }
        return result;
    }

    private static double Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out var value)
            ? value
            : 0;
}
