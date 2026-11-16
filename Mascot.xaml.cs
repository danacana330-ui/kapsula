using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kapsula;

/// <summary>Векторный робот-капсула: парит, моргает, гуляет и реагирует на происходящее вокруг.</summary>
public partial class Mascot : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly ScaleTransform _scale = new();
    private readonly RotateTransform _rotate = new() { CenterX = 20, CenterY = 16 };
    private readonly TranslateTransform _move = new();

    private double _t;
    private double _baseX;
    private double _baseY;
    private double _scaleBase = 1;
    private double _nextBlink = 2.5;
    private double _blinkUntil;
    private double _nextHop = 6;
    private double _hopUntil;
    private double _lookX;
    private double _lookY;
    private double _targetX;
    private double _targetY;

    public Mascot()
    {
        InitializeComponent();
        Root.RenderTransform = new TransformGroup { Children = { _scale, _rotate, _move } };
        _timer.Tick += (_, _) => Step();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        SizeChanged += (_, _) => Layout();
    }

    /// <summary>Насколько далеко гуляет влево-вправо (в пикселях контрола).</summary>
    public double Wander { get; set; }

    public bool Dancing { get; set; }

    /// <summary>Caps Lock включён — пугается и дрожит.</summary>
    public bool Scared { get; set; }

    /// <summary>Хозяина нет — спит стоя.</summary>
    public bool Asleep { get; set; }

    /// <summary>Что-то случилось (скопировал, докачалось) — прыгает от радости.</summary>
    public bool Excited { get; set; }

    /// <summary>Поворачивает глаза на курсор в любом месте экрана.</summary>
    public bool FollowCursor { get; set; }

    /// <summary>Меньше анимации: без прыжков и покачиваний.</summary>
    public bool Calm { get; set; }

    /// <summary>Взгляд в заданную сторону: -1..1 по обеим осям.</summary>
    public void LookAt(double nx, double ny)
    {
        _targetX = Math.Clamp(nx, -1, 1);
        _targetY = Math.Clamp(ny, -1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    public bool WearingHeadphones
    {
        get => Headphones.Opacity > 0.5;
        set => Headphones.Opacity = value ? 1 : 0;
    }

    private void Layout()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var s = Math.Min(ActualWidth / 40, ActualHeight / 32);
        _scaleBase = s;
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        _baseX = (ActualWidth - 40 * s) / 2;
        _baseY = (ActualHeight - 32 * s) / 2;
    }

    private void Step()
    {
        var dt = 0.033;
        _t += dt;

        // испугался — чуть подрос и отшатнулся
        var zoom = Scared && !Calm ? _scaleBase * 1.08 : _scaleBase;
        _scale.ScaleX += (zoom - _scale.ScaleX) * 0.2;
        _scale.ScaleY = _scale.ScaleX;

        if (FollowCursor) TrackCursor();
        else
        {
            _lookX += (_targetX - _lookX) * 0.18;
            _lookY += (_targetY - _lookY) * 0.18;
        }

        if (Asleep)
        {
            var sleepBob = Calm ? 0 : Math.Sin(_t * 0.9) * 0.5;
            _move.X = _baseX;
            _move.Y = _baseY - sleepBob;
            _rotate.Angle = Calm ? 0 : Math.Sin(_t * 0.4) * 0.8;
            SetEyes(0.55);
            Antenna.Opacity = 0.22;
            Shadow.Opacity = 0.2;
            Headphones.Opacity = Math.Min(Headphones.Opacity, 1);
            FootL.Height = 3.4;
            FootR.Height = 3.4;
            return;
        }

        var beat = Dancing ? 3.6 : 1.5;
        var bob = Calm ? 0 : Math.Abs(Math.Sin(_t * beat)) * (Dancing ? 2.2 : 0.9);
        var y = _baseY - bob;

        if (Scared)
        {
            _rotate.Angle = Calm ? 0 : Math.Sin(_t * 26) * 2.6;
            _move.X = _baseX + (Calm ? 0 : Math.Sin(_t * 40) * 0.7);
        }
        else if (Dancing)
        {
            _rotate.Angle = Calm ? 0 : Math.Sin(_t * beat / 2) * 7;
            _move.X = _baseX;
        }
        else
        {
            _rotate.Angle = Calm ? 0 : Math.Sin(_t * 0.7) * 1.5;

            if (!Calm && _t > _nextHop)
            {
                _hopUntil = _t + 0.34;
                _nextHop = _t + 6 + Random.Shared.NextDouble() * 5;
            }
            if (_t < _hopUntil)
            {
                var p = 1 - (_hopUntil - _t) / 0.34;
                y -= Math.Sin(p * Math.PI) * 4;
            }

            var range = Math.Min(Wander, Math.Max(0, (ActualWidth - 40 * _scale.ScaleX) / 2));
            _move.X = _baseX + (Calm ? 0 : Math.Sin(_t * 0.5) * range);
        }

        if (Excited && !Calm)
        {
            var p = (_t * 3.2) % 1;
            y -= Math.Sin(p * Math.PI) * 3.4;
        }

        _move.Y = y;

        FootL.Height = 3.4;
        FootR.Height = 3.4;
        Antenna.Opacity = 0.6 + 0.4 * Math.Abs(Math.Sin(_t * (Dancing ? 3.6 : 1.2)));
        Shadow.Opacity = Dancing ? 0.35 : 0.28;

        if (!Scared)
        {
            if (_t > _nextBlink)
            {
                _blinkUntil = _t + 0.12;
                _nextBlink = _t + 2.5 + Random.Shared.NextDouble() * 3;
            }
            SetEyes(_t < _blinkUntil ? 0.5 : 2.6);
        }
        else
        {
            SetEyes(4.6);
        }
    }

    private void TrackCursor()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        if (!GetCursorPos(out var p)) return;

        // координаты курсора берём в масштабе экрана, а не внутри контрола: иначе курсор,
        // стоящий далеко слева, всегда упирается в -1 и глаза застывают влево
        var center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
        var nx = Math.Clamp((p.X - center.X) / 420.0, -1, 1);
        var ny = Math.Clamp((p.Y - center.Y) / 540.0, -1, 1) * 0.5;
        _lookX += (nx - _lookX) * 0.22;
        _lookY += (ny - _lookY) * 0.22;
    }

    private void SetEyes(double height)
    {
        EyeL.Height = height;
        EyeR.Height = height;
        var top = 13.7 + (2.6 - height) / 2 + _lookY * 0.9;
        Canvas.SetTop(EyeL, top);
        Canvas.SetTop(EyeR, top);
        Canvas.SetLeft(EyeL, 16.2 + _lookX * 1.7);
        Canvas.SetLeft(EyeR, 21.2 + _lookX * 1.7);
    }
}
