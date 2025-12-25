/*
FIX: Image overlay needed zoom + pan without closing the overlay when clicking on the image.
CAUSE: Previous overlay used a Button wrapping the Image, so any click closed it and there was no zoom/pan.
CHANGE: Added ZoomPanImage UserControl with wheel-zoom + drag-pan + double-click reset. 2025-12-24
*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PromptLoom.Controls;

public partial class ZoomPanImage : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(ZoomPanImage),
        new PropertyMetadata(null, (_, __) => { }));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty ClickLeftCommandProperty = DependencyProperty.Register(
        nameof(ClickLeftCommand),
        typeof(ICommand),
        typeof(ZoomPanImage),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ClickRightCommandProperty = DependencyProperty.Register(
        nameof(ClickRightCommand),
        typeof(ICommand),
        typeof(ZoomPanImage),
        new PropertyMetadata(null));

    public ICommand? ClickLeftCommand
    {
        get => (ICommand?)GetValue(ClickLeftCommandProperty);
        set => SetValue(ClickLeftCommandProperty, value);
    }

    public ICommand? ClickRightCommand
    {
        get => (ICommand?)GetValue(ClickRightCommandProperty);
        set => SetValue(ClickRightCommandProperty, value);
    }

	public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
	    nameof(CloseCommand),
	    typeof(ICommand),
	    typeof(ZoomPanImage),
	    new PropertyMetadata(null));

	public ICommand? CloseCommand
	{
	    get => (ICommand?)GetValue(CloseCommandProperty);
	    set => SetValue(CloseCommandProperty, value);
	}

    private Point? _panStart;
    private Point _translateStart;
    private Point _mouseDown;
    private bool _didDrag;

    public ZoomPanImage()
    {
        InitializeComponent();

        Loaded += (_, __) =>
        {
            // Ensure we receive wheel events.
            Focusable = true;
            Focus();
        };

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseDoubleClick += (_, __) => Reset();
    }

    public void ZoomIn() => ZoomAtControlCenter(1.15);

    public void ZoomOut() => ZoomAtControlCenter(1.0 / 1.15);

    private void ZoomAtControlCenter(double factor)
    {
        var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        ApplyZoom(factor, center);
    }

    private void ApplyZoom(double factor, Point controlPoint)
    {
        var newScale = Math.Clamp(PART_Scale.ScaleX * factor, 0.15, 10.0);
        factor = newScale / PART_Scale.ScaleX;

        // Keep the point under the cursor (or center) stationary during zoom.
        var dx = (controlPoint.X - ActualWidth / 2.0) * (factor - 1.0);
        var dy = (controlPoint.Y - ActualHeight / 2.0) * (factor - 1.0);

        PART_Scale.ScaleX = newScale;
        PART_Scale.ScaleY = newScale;
        PART_Translate.X -= dx;
        PART_Translate.Y -= dy;
    }

    public void Reset()
    {
        PART_Scale.ScaleX = 1;
        PART_Scale.ScaleY = 1;
        PART_Translate.X = 0;
        PART_Translate.Y = 0;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        var p = e.GetPosition(this);
        ApplyZoom(factor, p);
    }


    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _panStart = e.GetPosition(this);
        _mouseDown = _panStart.Value;
        _didDrag = false;
        _translateStart = new Point(PART_Translate.X, PART_Translate.Y);
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
	    var upPos = e.GetPosition(this);

        _panStart = null;
        Cursor = Cursors.Arrow;
        ReleaseMouseCapture();

	    // If this was a click (not a drag) and happened in the letterboxed background (outside the
	    // displayed bitmap rect), treat it as "click outside" and request close.
	    if (!_didDrag && CloseCommand?.CanExecute(null) == true)
	    {
	        if (!IsPointOverDisplayedBitmap(upPos))
	        {
	            CloseCommand.Execute(null);
	            e.Handled = true;
	            return;
	        }
	    }
        e.Handled = true;
    }

	private bool IsPointOverDisplayedBitmap(Point p)
	{
	    // When the Image is Stretch=Uniform, the bitmap is letterboxed inside the control.
	    // Compute the displayed rect in control coordinates and account for our scale/translate.
	    if (Source is not BitmapSource bmp || ActualWidth <= 0 || ActualHeight <= 0)
	        return true; // If we can't compute, be permissive (don't close).

	    var iw = bmp.PixelWidth;
	    var ih = bmp.PixelHeight;
	    if (iw <= 0 || ih <= 0) return true;

	    var baseScale = Math.Min(ActualWidth / iw, ActualHeight / ih);
	    var dispW = iw * baseScale * PART_Scale.ScaleX;
	    var dispH = ih * baseScale * PART_Scale.ScaleY;

	    var cx = ActualWidth / 2.0 + PART_Translate.X;
	    var cy = ActualHeight / 2.0 + PART_Translate.Y;
	    var rect = new Rect(cx - dispW / 2.0, cy - dispH / 2.0, dispW, dispH);
	    return rect.Contains(p);
	}

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        var delta = current - _panStart.Value;

        if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 6)
            _didDrag = true;

        PART_Translate.X = _translateStart.X + delta.X;
        PART_Translate.Y = _translateStart.Y + delta.Y;
    }
}
