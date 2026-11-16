using System.Windows;

namespace Kapsula;

/// <summary>Короткое знакомство при первом запуске: три экрана, дальше не показывается.</summary>
public partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Body)[] Pages =
    {
        ("Капсула живёт наверху экрана",
            "Наведи мышь — раскроется. Свернуть обратно можно щелчком по полосе.\n\n" +
            "Alt+Space или Ctrl+K — поиск: программы, файлы, разделы капсулы и калькулятор в одном поле."),
        ("Что внутри",
            "Буфер обмена помнит скопированное, заметки лежат по папкам, таймер продолжает идти, даже если панель закрыть.\n\n" +
            "Alt+S — снимок экрана (сразу копируется в буфер), Alt+R — запись экрана со звуком."),
        ("ИИ и твои данные",
            "ИИ работает офлайн: модель скачивается прямо из приложения, один раз. Можно подключить и свой сервер — адрес, ключ и имя модели.\n\n" +
            "Копию всех данных (настройки, заметки, буфер, история чата) можно сохранить из меню в трее."),
    };

    private int _index;

    public OnboardingWindow()
    {
        InitializeComponent();
        Render();
    }

    private void Render()
    {
        var (title, body) = Pages[_index];
        TitleText.Text = title;
        BodyText.Text = body;
        StepText.Text = $"{_index + 1} из {Pages.Length}";
        NextBtn.Content = _index == Pages.Length - 1 ? "Начать" : "Дальше";
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_index < Pages.Length - 1)
        {
            _index++;
            Render();
            return;
        }
        Finish();
    }

    private void OnSkip(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        App.Settings.Onboarded = true;
        App.Settings.Save();
        Close();
    }
}
