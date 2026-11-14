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

    /// <summary>Показать плашку рядом с капсулой: справа, а если не помещается — слева.</summary>
    public void ShowNear(double capsuleLeft, double capsuleTop, double capsuleWidth, double capsuleHeight,
        string text = "ваша сводка готова", string glyph = "\uE946")
    {
        Label.Text = text;
        GlyphText.Text = glyph;
        Left = capsuleLeft;
        Top = capsuleTop;
        // размеры станут известны после первого показа — считаем по содержимому
        SizeToContent = SizeToContent.WidthAndHeight;
        Show();
        UpdateLayout();

        var work = SystemParameters.WorkArea;
        var rightSide = capsuleLeft + capsuleWidth + 6;
        var fitsRight = rightSide + ActualWidth <= work.Right;
        Left = fitsRight ? rightSide : capsuleLeft - ActualWidth - 6;
        Top = Math.Max(work.Top + 2,
            Math.Min(capsuleTop + (capsuleHeight - ActualHeight) / 2, work.Bottom - ActualHeight - 2));

        var slide = fitsRight ? 12 : -12;
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
