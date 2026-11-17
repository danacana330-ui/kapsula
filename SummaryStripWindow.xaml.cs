using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Kapsula;

/// <summary>Узкая плашка «ваша сводка готова» сбоку от капсулы. Висит до конца дня.</summary>
public partial class SummaryStripWindow : Window
{
    public event Action? Opened;

    public SummaryStripWindow()
    {
        InitializeComponent();
        Opacity = 0;
    }

    /// <summary>Показать плашку справа от капсулы; если справа нет места — под капсулой.</summary>
    public void ShowNear(double capsuleLeft, double capsuleTop, double capsuleWidth, double capsuleHeight,
        string text = "ваша сводка готова", string glyph = "\uE946")
    {
        Label.Text = text;
        GlyphText.Text = glyph;
        SizeToContent = SizeToContent.WidthAndHeight;
        Show();
        UpdateLayout();

        // у рамки Margin="10", поэтому окно шире самой плашки на 10 с каждой стороны
        const double pad = 10;
        const double gap = 6;
        const double barHeight = 46; // высота строки капсулы, к ней и прижимаемся

        var boxW = Box.ActualWidth;
        var boxH = Box.ActualHeight;
        var work = SystemParameters.WorkArea;

        var rightLeft = capsuleLeft + capsuleWidth + gap - pad;
        var fitsRight = rightLeft + pad + boxW <= work.Right - 2;

        double left, top;
        if (fitsRight)
        {
            left = rightLeft;
            top = capsuleTop + (barHeight - boxH) / 2 - pad;
        }
        else
        {
            left = capsuleLeft + (capsuleWidth - boxW) / 2 - pad;
            top = capsuleTop + capsuleHeight + gap - pad;
        }

        Left = Math.Clamp(left, work.Left + 2 - pad, Math.Max(work.Left + 2 - pad, work.Right - boxW - pad - 2));
        Top = Math.Clamp(top, work.Top + 2 - pad, Math.Max(work.Top + 2 - pad, work.Bottom - boxH - pad - 2));

        var slide = fitsRight ? 12 : 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(slide, 0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
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

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        Opened?.Invoke();
        HideNow();
    }
}
