using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace video_stream_researcher.Controls;

public partial class RangeSeekBar : UserControl
{
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<RangeSeekBar, TimeSpan>(nameof(Duration));

    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<RangeSeekBar, TimeSpan>(nameof(Position));

    public static readonly StyledProperty<TimeSpan> RangeStartProperty =
        AvaloniaProperty.Register<RangeSeekBar, TimeSpan>(nameof(RangeStart));

    public static readonly StyledProperty<TimeSpan> RangeEndProperty =
        AvaloniaProperty.Register<RangeSeekBar, TimeSpan>(nameof(RangeEnd));

    public static readonly StyledProperty<bool> RangeSelectionEnabledProperty =
        AvaloniaProperty.Register<RangeSeekBar, bool>(nameof(RangeSelectionEnabled));

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public TimeSpan Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan RangeStart
    {
        get => GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }

    public TimeSpan RangeEnd
    {
        get => GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
    }

    public bool RangeSelectionEnabled
    {
        get => GetValue(RangeSelectionEnabledProperty);
        set => SetValue(RangeSelectionEnabledProperty, value);
    }

    public event Action<TimeSpan>? SeekRequested;
    public event Action<TimeSpan, TimeSpan>? RangeChanged;

    private enum DragMode
    {
        None,
        TrackSeek,
        StartThumb,
        EndThumb
    }

    private DragMode _dragMode;

    public RangeSeekBar()
    {
        InitializeComponent();

        Root.PointerPressed += OnPointerPressed;
        Root.PointerMoved += OnPointerMoved;
        Root.PointerReleased += OnPointerReleased;
        Root.SizeChanged += (_, _) => LayoutNow();

        this.GetObservable(DurationProperty).Subscribe(_ => LayoutNow());
        this.GetObservable(PositionProperty).Subscribe(_ => LayoutNow());
        this.GetObservable(RangeStartProperty).Subscribe(_ => LayoutNow());
        this.GetObservable(RangeEndProperty).Subscribe(_ => LayoutNow());
        this.GetObservable(RangeSelectionEnabledProperty).Subscribe(_ => LayoutNow());
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(Root);
        if (IsOnThumb(p, StartThumb))
        {
            _dragMode = DragMode.StartThumb;
        }
        else if (IsOnThumb(p, EndThumb))
        {
            _dragMode = DragMode.EndThumb;
        }
        else
        {
            _dragMode = DragMode.TrackSeek;
            SeekFromPoint(p.X);
        }

        e.Pointer.Capture(Root);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        var p = e.GetPosition(Root);
        if (_dragMode == DragMode.TrackSeek)
        {
            SeekFromPoint(p.X);
            return;
        }

        if (!RangeSelectionEnabled)
        {
            return;
        }

        var t = TimeFromX(p.X);
        if (_dragMode == DragMode.StartThumb)
        {
            var start = Clamp(t);
            var end = RangeEnd;
            if (start > end)
            {
                start = end;
            }
            RangeStart = start;
            RangeChanged?.Invoke(start, end);
            LayoutNow();
        }
        else if (_dragMode == DragMode.EndThumb)
        {
            var start = RangeStart;
            var end = Clamp(t);
            if (end < start)
            {
                end = start;
            }
            RangeEnd = end;
            RangeChanged?.Invoke(start, end);
            LayoutNow();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void SeekFromPoint(double x)
    {
        var t = TimeFromX(x);
        t = Clamp(t);
        SeekRequested?.Invoke(t);
    }

    private TimeSpan Clamp(TimeSpan t)
    {
        var d = Duration;
        if (d <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }
        if (t < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }
        if (t > d)
        {
            return d;
        }
        return t;
    }

    private bool IsOnThumb(Point p, Control thumb)
    {
        var x = Canvas.GetLeft(thumb);
        var y = Canvas.GetTop(thumb);
        if (double.IsNaN(x))
        {
            x = 0;
        }
        if (double.IsNaN(y))
        {
            y = 0;
        }
        var rect = new Rect(x - 6, y - 6, thumb.Bounds.Width + 12, thumb.Bounds.Height + 12);
        return rect.Contains(p);
    }

    private TimeSpan TimeFromX(double x)
    {
        var w = Root.Bounds.Width;
        if (w <= 1)
        {
            return TimeSpan.Zero;
        }
        var p = x / w;
        p = Math.Max(0, Math.Min(1, p));
        return TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * p);
    }

    private void LayoutNow()
    {
        var w = Root.Bounds.Width;
        var h = Root.Bounds.Height;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        var centerY = h / 2;

        Canvas.SetLeft(Track, 0);
        Canvas.SetTop(Track, centerY - Track.Bounds.Height / 2);
        Track.Width = w;

        var duration = Duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : Duration;

        var start = RangeSelectionEnabled ? RangeStart : TimeSpan.Zero;
        var end = RangeSelectionEnabled ? RangeEnd : duration;
        start = Clamp(start);
        end = Clamp(end);
        if (end < start)
        {
            end = start;
        }

        var startX = w * (start.TotalMilliseconds / duration.TotalMilliseconds);
        var endX = w * (end.TotalMilliseconds / duration.TotalMilliseconds);
        var selWidth = Math.Max(0, endX - startX);

        Selection.Width = selWidth;
        Canvas.SetLeft(Selection, startX);
        Canvas.SetTop(Selection, centerY - Selection.Bounds.Height / 2);

        StartThumb.IsVisible = RangeSelectionEnabled;
        EndThumb.IsVisible = RangeSelectionEnabled;

        Canvas.SetLeft(StartThumb, startX - StartThumb.Width / 2);
        Canvas.SetTop(StartThumb, centerY - StartThumb.Height / 2);

        Canvas.SetLeft(EndThumb, endX - EndThumb.Width / 2);
        Canvas.SetTop(EndThumb, centerY - EndThumb.Height / 2);

        var pos = Clamp(Position);
        var posX = w * (pos.TotalMilliseconds / duration.TotalMilliseconds);
        Canvas.SetLeft(Playhead, posX - Playhead.Width / 2);
        Canvas.SetTop(Playhead, centerY - Playhead.Height / 2);
    }
}

