using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Kapsula.Services;

namespace Kapsula;

/// <summary>Маленькая карточка «скопировано» с быстрыми действиями.</summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromSeconds(6) };
    private string _text = "";

    public ToastWindow()
    {
        InitializeComponent();
        _hide.Tick += (_, _) =>
        {
            _hide.Stop();
            HideNow();
        };
    }

    public void ShowText(string text)
    {
        _text = text;
        var preview = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (preview.Length > 160) preview = preview[..160] + "…";
        Preview.Text = preview;

        var isLink = text.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !text.Contains(' ');
        OpenButton.Visibility = isLink ? Visibility.Visible : Visibility.Collapsed;

        var width = Math.Min(340, Math.Max(260, Preview.ActualWidth + 60));
        Left = SystemParameters.PrimaryScreenWidth - width - 24;
        Top = 66;

        if (!IsVisible) Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
        _hide.Stop();
        _hide.Start();
    }

    public void HideNow()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
        fade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void OnToNotes(object sender, RoutedEventArgs e)
    {
        if (_text.Length > 0) NotesStore.Add(_text);
        HideNow();
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_text) { UseShellExecute = true });
        }
        catch
        {
        }
        HideNow();
    }
}
