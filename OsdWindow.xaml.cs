using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Kapsula;

/// <summary>Всплывающий индикатор поверх экрана: громкость, CapsLock, запись.</summary>
public partial class OsdWindow : Window
{
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(1400) };

    public OsdWindow()
    {
        InitializeComponent();
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 58;
        Opacity = 0;
        _hide.Tick += (_, _) =>
        {
            _hide.Stop();
            HideNow();
        };
    }

    public void Show(string glyph, string title, string value, bool sticky = false)
    {
        Glyph.Text = glyph;
        Title.Text = title;
        Value.Text = value;
        if (!IsVisible) Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        _hide.Stop();
        if (!sticky) _hide.Start();
    }

    public void HideNow()
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Hide();
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
