using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kapsula.Services;

namespace Kapsula.Panels;

public sealed class ChatBubble : INotifyPropertyChanged
{
    private string _text = "";

    public bool IsUser { get; init; }
    public string Align => IsUser ? HorizontalAlignment.Right.ToString() : HorizontalAlignment.Left.ToString();

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class AiPanel : UserControl
{
    public event Action? Back;

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly AiService _ai;
    private readonly List<AiMessage> _history = new();
    private readonly ObservableCollection<ChatBubble> _bubbles = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public AiPanel()
    {
        InitializeComponent();
        _ai = new AiService(_settings.AiEndpoint);
        ChatList.ItemsSource = _bubbles;

        var (model, why) = AiService.Recommend();
        AdviceText.Text = $"{AiService.RamGb():0} ГБ ОЗУ · советуем {model} ({why})";
        PullBox.Text = model;

        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        if (!await _ai.IsRunningAsync())
        {
            HintText.Text = "Ollama не запущена";
            _bubbles.Add(new ChatBubble
            {
                IsUser = false,
                Text = "Локальный ИИ не отвечает. Поставь Ollama (ollama.com) — приложение подхватит её само. " +
                       "Или запусти её командой «ollama serve» и открой этот экран заново.",
            });
            return;
        }

        var models = (await _ai.ListAsync())
            .Where(m => !m.Name.Contains("embed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        ModelsList.ItemsSource = models;
        HintText.Text = models.Count == 0 ? "моделей пока нет" : $"{models.Count} моделей";

        if (models.Count == 0)
        {
            _bubbles.Add(new ChatBubble
            {
                IsUser = false,
                Text = "Ollama работает, но моделей нет. Скачай подходящую кнопкой слева — например " +
                       AiService.Recommend().Model + ".",
            });
            return;
        }

        var chosen = models.FirstOrDefault(m => m.Name == _settings.AiModel)
                     ?? models.FirstOrDefault(m => !m.Name.Contains("cloud", StringComparison.OrdinalIgnoreCase))
                     ?? models[0];
        ModelsList.SelectedItem = chosen;
    }

    private void OnModelPicked(object sender, SelectionChangedEventArgs e)
    {
        if (ModelsList.SelectedItem is OllamaModel model)
        {
            _settings.AiModel = model.Name;
            _settings.Save();
            HintText.Text = model.Name;
        }
    }

    private async void OnSend(object sender, RoutedEventArgs e) => await SendAsync();

    private async void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_busy) return;
        var text = Input.Text.Trim();
        if (text.Length == 0) return;
        if (ModelsList.SelectedItem is not OllamaModel model)
        {
            HintText.Text = "сначала выбери модель";
            return;
        }

        Input.Text = "";
        _bubbles.Add(new ChatBubble { IsUser = true, Text = text });
        _history.Add(new AiMessage { Role = "user", Content = text });

        var answer = new ChatBubble { IsUser = false, Text = "" };
        _bubbles.Add(answer);
        ChatScroll.ScrollToEnd();

        _busy = true;
        _cts = new CancellationTokenSource();
        try
        {
            var full = await _ai.ChatAsync(model.Name, _history, piece =>
            {
                answer.Text += piece;
                ChatScroll.ScrollToEnd();
            }, _cts.Token);

            _history.Add(new AiMessage { Role = "assistant", Content = full });
            if (answer.Text.Length == 0) answer.Text = "(пустой ответ)";
        }
        finally
        {
            _busy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void OnPull(object sender, RoutedEventArgs e)
    {
        var name = PullBox.Text.Trim();
        if (name.Length == 0) return;
        PullBar.Visibility = Visibility.Visible;
        PullStatus.Text = "начинаю скачивание…";
        try
        {
            await _ai.PullAsync(name, status =>
            {
                PullStatus.Text = status;
                var digits = new string(status.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                if (int.TryParse(digits, out var percent)) PullBar.Value = percent;
            }, CancellationToken.None);
            PullStatus.Text = "готово: " + name;
            await InitAsync();
        }
        catch (Exception ex)
        {
            PullStatus.Text = "не вышло: " + ex.Message;
        }
    }

    private void OnNewChat(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _history.Clear();
        _bubbles.Clear();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Back?.Invoke();
    }
}
