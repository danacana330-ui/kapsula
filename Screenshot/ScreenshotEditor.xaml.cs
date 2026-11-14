using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Kapsula.Screenshot;

public partial class ScreenshotEditor : Window
{
    private readonly BitmapSource _shot;
    private Color _color = Color.FromRgb(0xFF, 0x7A, 0x6B);
    private double _thickness = 4;

    public ScreenshotEditor(BitmapSource shot)
    {
        InitializeComponent();
        _shot = shot;
        Shot.Source = shot;

        Surface.Width = shot.PixelWidth;
        Surface.Height = shot.PixelHeight;

        Ink.Strokes.Clear();
        ApplyBrush();

        var maxW = SystemParameters.WorkArea.Width * 0.9;
        var maxH = SystemParameters.WorkArea.Height * 0.82;
        var scale = Math.Min(1, Math.Min(maxW / shot.PixelWidth, maxH / shot.PixelHeight));
        Width = Math.Max(480, shot.PixelWidth * scale + 24);
        Height = Math.Max(320, shot.PixelHeight * scale + 110);

        PreviewKeyDown += OnKey;
    }

    private void ApplyBrush()
    {
        var attrs = Ink.DefaultDrawingAttributes;
        attrs.Color = _color;
        attrs.Width = _thickness;
        attrs.Height = _thickness;
        attrs.FitToCurve = true;
        attrs.IgnorePressure = true;
    }

    private void OnColor(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex })
        {
            _color = (Color)ColorConverter.ConvertFromString(hex);
            ApplyBrush();
        }
    }

    private void OnThin(object sender, RoutedEventArgs e)
    {
        _thickness = 3;
        ApplyBrush();
    }

    private void OnThick(object sender, RoutedEventArgs e)
    {
        _thickness = 9;
        ApplyBrush();
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (Ink.Strokes.Count > 0) Ink.Strokes.RemoveAt(Ink.Strokes.Count - 1);
    }

    private void OnClear(object sender, RoutedEventArgs e) => Ink.Strokes.Clear();

    private BitmapSource Render()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_shot, new Rect(0, 0, _shot.PixelWidth, _shot.PixelHeight));
            var brush = new VisualBrush(Ink) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
            dc.DrawRectangle(brush, null, new Rect(0, 0, _shot.PixelWidth, _shot.PixelHeight));
        }

        var target = new RenderTargetBitmap(_shot.PixelWidth, _shot.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetImage(Render());
        }
        catch
        {
        }
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"снимок_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog() != true) return;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render()));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnFindOnline(object sender, RoutedEventArgs e)
    {
        FindButton.IsEnabled = false;
        Hint.Text = "Загружаю снимок в поиск…";

        var link = await Services.ReverseImageSearch.FindAsync(ToPng(Render()));

        FindButton.IsEnabled = true;
        if (link is null)
        {
            Hint.Text = "Не получилось отправить снимок — нет связи с поиском";
            return;
        }

        Hint.Text = "Открыл поиск в браузере — вернись сюда, снимок ещё здесь";
        _lastLink = link;
        CopyLinkButton.Visibility = Visibility.Visible;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            Hint.Text = "Браузер не открылся — скопируй ссылку и открой её вручную";
        }
    }

    private string? _lastLink;

    private void OnCopyLink(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastLink)) return;
        try
        {
            Clipboard.SetText(_lastLink);
            Hint.Text = "Ссылка скопирована — вставь её в адресную строку браузера";
        }
        catch
        {
            Hint.Text = "Не получилось скопировать: " + _lastLink;
        }
    }

    private static byte[] ToPng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) OnCopy(sender, e);
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) OnSave(sender, e);
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) OnFindOnline(sender, e);
    }
}
