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

/// <summary>Строка в списке моделей: либо мини-модель, скачанная капсулой, либо модель Ollama.</summary>
public sealed class AiChoice
{
    public string Kind { get; init; } = "ollama";
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public MiniModel? Mini { get; init; }
}

public partial class AiPanel : UserControl
{
    private const string ChatFile = "ai_chat.json";
    private const int MaxSaved = 40;

    public event Action? Back;

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly AiService _ai;
    private readonly MiniAiService _mini = new(MiniRuntime.Server);
    private readonly List<AiMessage> _history = new();
    private readonly ObservableCollection<ChatBubble> _bubbles = new();
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _ollamaOk;
    private AiChoice? _choice;

    public AiPanel()
    {
        InitializeComponent();
        _ai = new AiService(_settings.AiEndpoint);
        ChatList.ItemsSource = _bubbles;
        RestoreChat();

        var (model, why) = AiService.Recommend();
        AdviceText.Text = $"{AiService.RamGb():0} ГБ ОЗУ · советуем {model} ({why})";
        PullBox.Text = model;

        Loaded += async (_, _) => await InitAsync();
    }

    private void RestoreChat()
    {
        foreach (var message in Store.Load<List<AiMessage>>(ChatFile, new List<AiMessage>()))
        {
            if (string.IsNullOrWhiteSpace(message.Content)) continue;
            _history.Add(message);
            _bubbles.Add(new ChatBubble { IsUser = message.Role == "user", Text = message.Content });
        }
    }

    private void PersistChat()
    {
        if (_history.Count > MaxSaved) _history.RemoveRange(0, _history.Count - MaxSaved);
        Store.Save(ChatFile, _history);
    }

    private async Task InitAsync()
    {
        var choices = new List<AiChoice>();

        foreach (var mini in MiniModels.All)
        {
            choices.Add(new AiChoice
            {
                Kind = "mini",
                Id = mini.Id,
                Title = mini.Title,
                Subtitle = MiniModelStore.IsInstalled(mini)
                    ? $"{MiniModelStore.SizeText(mini)} · на компьютере"
                    : $"{mini.Note} · не скачана",
                Mini = mini,
            });
        }

        _ollamaOk = await _ai.IsRunningAsync();
        if (_ollamaOk)
        {
            var models = (await _ai.ListAsync())
                .Where(m => !m.Name.Contains("embed", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var m in models)
            {
                choices.Add(new AiChoice
                {
                    Kind = "ollama",
                    Id = m.Name,
                    Title = m.Name,
                    Subtitle = $"{m.SizeText} · Ollama",
                });
            }
        }

        ModelsList.ItemsSource = choices;
        HintText.Text = _ollamaOk ? "Ollama на связи" : "своя мини-модель";

        var installed = choices.FirstOrDefault(c => c.Kind == "mini" && c.Mini is not null &&
                                                   MiniModelStore.IsInstalled(c.Mini));
        var saved = choices.FirstOrDefault(c => c.Kind == "ollama" && c.Id == _settings.AiModel);
        ModelsList.SelectedItem = installed ?? saved ?? choices.FirstOrDefault();
    }

    private void OnModelPicked(object sender, SelectionChangedEventArgs e)
    {
        _choice = ModelsList.SelectedItem as AiChoice;
        UpdateMiniControls();

        if (_choice is { Kind: "ollama" })
        {
            _settings.AiModel = _choice.Id;
            _settings.Save();
        }

        OllamaBanner.Visibility = _ollamaOk && _choice is { Kind: "mini" }
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateMiniControls()
    {
        var mini = _choice?.Mini;
        if (mini is null)
        {
            MiniActionBtn.Visibility = Visibility.Collapsed;
            OllamaPullBlock.Visibility = Visibility.Visible;
            return;
        }

        MiniActionBtn.Visibility = Visibility.Visible;
        OllamaPullBlock.Visibility = Visibility.Collapsed;
        MiniActionBtn.Content = MiniModelStore.IsInstalled(mini) ? "Удалить" : "Скачать";
        HintText.Text = MiniModelStore.IsInstalled(mini)
            ? $"{mini.Title} готова"
            : $"{mini.Title} · {mini.Note}";
    }

    private async void OnMiniAction(object sender, RoutedEventArgs e)
    {
        if (_choice?.Mini is not { } mini) return;

        if (MiniModelStore.IsInstalled(mini))
        {
            if (MiniRuntime.Server.ModelPath == MiniModelStore.FilePath(mini)) MiniRuntime.Server.Stop();
            MiniModelStore.Delete(mini);
            PullStatus.Text = "модель удалена";
            await InitAsync();
            return;
        }

        await DownloadMiniAsync(mini);
    }

    private async Task DownloadMiniAsync(MiniModel mini)
    {
        MiniActionBtn.IsEnabled = false;
        PullBar.Visibility = Visibility.Visible;
        PullBar.Value = 0;
        PullStatus.Text = "скачиваю " + mini.Title + "…";

        var app = Application.Current as App;
        var ok = await MiniModelStore.DownloadAsync(mini, percent =>
        {
            PullBar.Value = percent;
            PullStatus.Text = $"скачиваю {mini.Title} — {percent}%";
            app?.SetTrayStatus($"Kapsula — скачивание {percent}%");
        }, CancellationToken.None);

        app?.SetTrayStatus(null);
        MiniActionBtn.IsEnabled = true;
        PullStatus.Text = ok ? "готово: " + mini.Title : "не удалось скачать — проверьте интернет";
        if (!ok) PullBar.Visibility = Visibility.Collapsed;

        await InitAsync();
    }

    private void OnOllamaSwitch(object sender, RoutedEventArgs e)
    {
        var ollama = (ModelsList.ItemsSource as List<AiChoice>)?
            .FirstOrDefault(c => c.Kind == "ollama");
        if (ollama is null) return;

        ModelsList.SelectedItem = ollama;
        OllamaBanner.Visibility = Visibility.Collapsed;
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
        if (_choice is null)
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
        SendBtn.IsEnabled = false;
        StopBtn.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        try
        {
            string full;
            if (_choice.Kind == "ollama")
            {
                full = await _ai.ChatAsync(_choice.Id, _history, piece =>
                {
                    answer.Text += piece;
                    ChatScroll.ScrollToEnd();
                }, _cts.Token);
            }
            else
            {
                var ready = await EnsureMiniAsync(_choice.Mini, _cts.Token);
                if (!ready)
                {
                    answer.Text = "(мини-модель не запустилась)";
                    return;
                }

                full = await _mini.ChatAsync(_history, piece =>
                {
                    answer.Text += piece;
                    ChatScroll.ScrollToEnd();
                }, _cts.Token);
            }

            _history.Add(new AiMessage { Role = "assistant", Content = full });
            if (answer.Text.Length == 0) answer.Text = "(пустой ответ)";
            PersistChat();
        }
        finally
        {
            _busy = false;
            SendBtn.IsEnabled = true;
            StopBtn.Visibility = Visibility.Collapsed;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>Скачать движок (если нужно), проверить модель и поднять llama-server.</summary>
    private async Task<bool> EnsureMiniAsync(MiniModel? mini, CancellationToken token)
    {
        if (mini is null) return false;

        var path = MiniModelStore.FilePath(mini);
        if (!MiniModelStore.IsInstalled(mini))
        {
            HintText.Text = "сначала скачай модель";
            return false;
        }

        if (MiniRuntime.Server.IsRunning && MiniRuntime.Server.ModelPath == path) return true;

        if (!LlamaEngine.IsInstalled)
        {
            PullBar.Visibility = Visibility.Visible;
            PullBar.Value = 0;
            PullStatus.Text = "скачиваю движок…";
            var engineOk = await LlamaEngine.EnsureAsync(percent =>
            {
                PullBar.Value = percent;
                PullStatus.Text = $"скачиваю движок — {percent}%";
            }, token);
            if (!engineOk)
            {
                PullStatus.Text = "движок не скачался";
                return false;
            }
        }

        HintText.Text = "запускаю модель…";
        PullStatus.Text = "первый запуск занимает несколько секунд";
        var ok = await MiniRuntime.Server.StartAsync(path, token);
        PullBar.Visibility = Visibility.Collapsed;
        HintText.Text = ok ? $"{mini.Title} работает" : "модель не запустилась";
        PullStatus.Text = ok ? "" : "проверьте свободное место и антивирус";
        return ok;
    }

    private void OnStop(object sender, RoutedEventArgs e) => _cts?.Cancel();

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
        PersistChat();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Back?.Invoke();
    }
}
