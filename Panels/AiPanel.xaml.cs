using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kapsula.Services;

namespace Kapsula.Panels;

public sealed class ChatBubble : INotifyPropertyChanged
{
    private string _text = "";
    private string _meta = "";

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

    /// <summary>Подпись под сообщением: время ответа или пометка «команда».</summary>
    public string Meta
    {
        get => _meta;
        set
        {
            _meta = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Meta)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Строка в списке моделей: мини-модель капсулы, Ollama, облако или свой сервер.</summary>
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
    private bool _cloudOk;
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

        ServerUrl.Text = _settings.AiServerUrl;
        ServerKey.Text = _settings.AiServerKey;
        ServerModel.Text = _settings.AiServerModel;

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

        // Облако: одна строка «авто» сразу после мини-моделей, без списка названий.
        var cloud = await FreeCloud.ListAsync(_settings.AiGatewayPort);
        _cloudOk = cloud.Any(m => m.Name == "free-auto");
        if (_cloudOk)
        {
            choices.Add(new AiChoice
            {
                Kind = "cloud",
                Id = "free-auto",
                Title = "Бесплатная · авто",
                Subtitle = "облако · нужен интернет",
            });
        }

        // Свой сервер (например, платная модель по своему ключу) — тоже наверху, до списка Ollama.
        if (!string.IsNullOrWhiteSpace(_settings.AiServerUrl))
        {
            var model = string.IsNullOrWhiteSpace(_settings.AiServerModel) ? "модель не указана" : _settings.AiServerModel;
            choices.Add(new AiChoice
            {
                Kind = "server",
                Id = "server:" + model,
                Title = model,
                Subtitle = "свой сервер · " + HostOf(_settings.AiServerUrl),
            });
        }

        _ollamaOk = await _ai.IsRunningAsync();
        if (_ollamaOk)
        {
            var models = (await _ai.ListAsync())
                .Where(m => !m.Name.Contains("embed", StringComparison.OrdinalIgnoreCase))
                // free-* — модели шлюза облака, выше они показаны одной строкой
                .Where(m => !m.Name.StartsWith("free-", StringComparison.OrdinalIgnoreCase))
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

        var saved = choices.FirstOrDefault(c => c.Id == _settings.AiModel);
        var installed = choices.FirstOrDefault(c => c.Kind == "mini" && c.Mini is not null &&
                                                   MiniModelStore.IsInstalled(c.Mini));
        ModelsList.SelectedItem = saved ?? installed ?? choices.FirstOrDefault();

        if (!_cloudOk && !_ollamaOk)
            HintText.Text = "своя мини-модель";
        else if (!_cloudOk)
            HintText.Text = "Ollama на связи";
    }

    private static string HostOf(string url)
    {
        try
        {
            return new Uri(url.Contains("://") ? url : "https://" + url).Host;
        }
        catch
        {
            return url;
        }
    }

    private void OnModelPicked(object sender, SelectionChangedEventArgs e)
    {
        _choice = ModelsList.SelectedItem as AiChoice;
        if (_choice is null) return;

        UpdateMiniControls();

        _settings.AiModel = _choice.Id;
        _settings.Save();

        OllamaBanner.Visibility = _ollamaOk && _choice.Kind == "mini"
            ? Visibility.Visible
            : Visibility.Collapsed;

        HintText.Text = _choice.Kind switch
        {
            "cloud" => "бесплатная облачная модель · нужен интернет",
            "server" => "свой сервер · " + HostOf(_settings.AiServerUrl),
            "ollama" => "Ollama на связи",
            _ => MiniModelStore.IsInstalled(_choice.Mini!) ? $"{_choice.Title} готова" : $"{_choice.Title} · {_choice.Mini!.Note}",
        };
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

    // ── свой сервер ─────────────────────────────────────────────────────────

    private void OnServerSave(object sender, RoutedEventArgs e)
    {
        _settings.AiServerUrl = ServerUrl.Text.Trim();
        _settings.AiServerKey = ServerKey.Text.Trim();
        _settings.AiServerModel = ServerModel.Text.Trim();
        _settings.Save();

        ServerStatus.Text = string.IsNullOrWhiteSpace(_settings.AiServerUrl)
            ? "адрес пустой — сервер отключён"
            : "сохранено";

        if (!string.IsNullOrWhiteSpace(_settings.AiServerUrl))
            _settings.AiModel = "server:" + _settings.AiServerModel;
        _settings.Save();

        _ = InitAsync();
    }

    private async void OnServerTest(object sender, RoutedEventArgs e)
    {
        var url = ServerUrl.Text.Trim();
        if (url.Length == 0)
        {
            ServerStatus.Text = "сначала впиши адрес";
            return;
        }

        ServerTestBtn.IsEnabled = false;
        ServerStatus.Text = "проверяю…";
        var (models, error) = await CustomServer.ListAsync(url, ServerKey.Text.Trim(), CancellationToken.None);
        ServerTestBtn.IsEnabled = true;

        if (error is not null)
        {
            ServerStatus.Text = "не работает: " + error;
            return;
        }

        if (models.Count == 0)
        {
            ServerStatus.Text = "сервер ответил, но моделей в списке нет — впиши имя вручную";
            return;
        }

        ServerStatus.Text = $"работает, моделей: {models.Count} · {string.Join(", ", models.Take(3))}";
        if (string.IsNullOrWhiteSpace(ServerModel.Text)) ServerModel.Text = models[0];
    }

    // ── чат ─────────────────────────────────────────────────────────────────

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
        var text = Input.Text.Trim();
        if (text.Length == 0) return;
        Input.Text = "";
        await RunAsync(text, echoUser: true);
    }

    private async Task RunAsync(string text, bool echoUser)
    {
        if (_busy) return;

        if (echoUser)
        {
            _bubbles.Add(new ChatBubble { IsUser = true, Text = text });
            _history.Add(new AiMessage { Role = "user", Content = text });
        }

        var answer = new ChatBubble { IsUser = false, Text = "" };
        _bubbles.Add(answer);
        ChatScroll.ScrollToEnd();

        // простые команды понимаем сами, без модели — быстро и офлайн
        if (QuickCommands.TryHandle(text, out var quickReply))
        {
            answer.Text = quickReply;
            answer.Meta = "команда";
            _history.Add(new AiMessage { Role = "assistant", Content = quickReply });
            PersistChat();
            return;
        }

        if (_choice is null)
        {
            answer.Text = "Сначала выбери модель слева.";
            return;
        }

        _busy = true;
        SendBtn.IsEnabled = false;
        RegenBtn.IsEnabled = false;
        StopBtn.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        try
        {
            string full;
            if (_choice.Kind == "cloud")
            {
                full = await FreeCloud.ChatAsync(_settings.AiGatewayPort, _choice.Id, _history, piece =>
                {
                    answer.Text += piece;
                    ChatScroll.ScrollToEnd();
                }, _cts.Token);
            }
            else if (_choice.Kind == "server")
            {
                full = await CustomServer.ChatAsync(_settings.AiServerUrl, _settings.AiServerKey,
                    _settings.AiServerModel, _history, piece =>
                    {
                        answer.Text += piece;
                        ChatScroll.ScrollToEnd();
                    }, _cts.Token);
            }
            else if (_choice.Kind == "ollama")
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
                    answer.Text = "Мини-модель не запустилась. Проверь свободное место и скачивание.";
                    return;
                }

                full = await _mini.ChatAsync(_history, piece =>
                {
                    answer.Text += piece;
                    ChatScroll.ScrollToEnd();
                }, _cts.Token);
            }

            watch.Stop();
            if (answer.Text.Length == 0) answer.Text = "(пустой ответ)";
            answer.Meta = $"{watch.Elapsed.TotalSeconds:0.0} с";
            _history.Add(new AiMessage { Role = "assistant", Content = full });
            PersistChat();
        }
        finally
        {
            _busy = false;
            SendBtn.IsEnabled = true;
            RegenBtn.IsEnabled = true;
            StopBtn.Visibility = Visibility.Collapsed;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>Повторить последний вопрос: убираем прошлый ответ и спрашиваем снова.</summary>
    private async void OnRegenerate(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var lastUser = _history.LastOrDefault(m => m.Role == "user");
        if (lastUser is null) return;

        while (_history.Count > 0 && _history[^1].Role != "user") _history.RemoveAt(_history.Count - 1);
        while (_bubbles.Count > 0 && !_bubbles[^1].IsUser) _bubbles.RemoveAt(_bubbles.Count - 1);
        PersistChat();

        await RunAsync(lastUser.Content, echoUser: false);
    }

    private void OnCopyBubble(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ChatBubble bubble }) return;
        try
        {
            Clipboard.SetText(bubble.Text);
        }
        catch
        {
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
