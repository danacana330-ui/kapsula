using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kapsula.Screenshot;

public partial class RegionOverlay : Window
{
    public event Action<BitmapSource?>? Finished;

    private Point _start;
    private bool _dragging;

    public RegionOverlay()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        PreviewKeyDown += OnKey;
        Loaded += (_, _) =>
        {
            // окно поверх topmost-капсулы: без повторного поднятия Esc не доходит
            Topmost = false;
            Topmost = true;
            Activate();
            Focus();
            Keyboard.Focus(this);
        };
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        _dragging = true;
        Selection.Visibility = Visibility.Visible;
        Canvas.SetLeft(Selection, _start.X);
        Canvas.SetTop(Selection, _start.Y);
        Selection.Width = 0;
        Selection.Height = 0;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(Root);
        var x = Math.Min(p.X, _start.X);
        var y = Math.Min(p.Y, _start.Y);
        var w = Math.Abs(p.X - _start.X);
        var h = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(Selection, x);
        Canvas.SetTop(Selection, y);
        Selection.Width = w;
        Selection.Height = h;
        SizeLabel.Text = $"{(int)w} × {(int)h}";
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;

        var rect = new Rect(Canvas.GetLeft(Selection), Canvas.GetTop(Selection), Selection.Width, Selection.Height);
        if (rect.Width < 4 || rect.Height < 4)
        {
            Close();
            Finished?.Invoke(null);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var screenX = (int)Math.Round((SystemParameters.VirtualScreenLeft + rect.X) * dpi.DpiScaleX);
        var screenY = (int)Math.Round((SystemParameters.VirtualScreenTop + rect.Y) * dpi.DpiScaleY);
        var pixelW = (int)Math.Round(rect.Width * dpi.DpiScaleX);
        var pixelH = (int)Math.Round(rect.Height * dpi.DpiScaleY);

        Close();

        try
        {
            using var bmp = new System.Drawing.Bitmap(pixelW, pixelH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenX, screenY, 0, 0, new System.Drawing.Size(pixelW, pixelH),
                    System.Drawing.CopyPixelOperation.SourceCopy);
            }

            var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = ms;
            source.EndInit();
            source.Freeze();
            Finished?.Invoke(source);
        }
        catch
        {
            Finished?.Invoke(null);
        }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            Finished?.Invoke(null);
        }
    }
}
