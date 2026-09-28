using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FocusTone.Models;
namespace FocusTone.Controls;
public sealed class WaveformControl : FrameworkElement
{
    public float[] Peaks { get; set; } = [];
    public double Duration { get; set; } = 1;
    public double Start { get; set; }
    public double End { get; set; } = 1;
    public double Position { get; set; }
    public event Action? RangeChanged;
    public event Action<double>? SeekRequested;
    private int drag;
    public WaveformControl() { MinHeight = 200; Cursor = Cursors.Hand; Focusable = true; ToolTip = "点击或拖动白色指针跳转；拖动蓝色边界调整片段。精确时间可在下方输入。"; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth; var h = ActualHeight; if (w <= 0 || h <= 0) return;
        double X(double value) => Math.Clamp(value / Math.Max(.001, Duration), 0, 1) * w;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(36, 36, 36)), null, new Rect(0, 0, w, h), 8, 8);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(38, 96, 205, 255)), null, new Rect(X(Start), 8, Math.Max(0, X(End) - X(Start)), h - 38));
        var muted = new Pen(new SolidColorBrush(Color.FromRgb(90, 90, 90)), 1.4); var active = new Pen(new SolidColorBrush(Color.FromRgb(96, 205, 255)), 1.5);
        var center = (h - 30) / 2;
        for (int px = 2; px < w - 2; px += 3)
        {
            var i = Math.Min(Peaks.Length - 1, (int)(px / w * Peaks.Length)); var amplitude = i >= 0 ? Math.Max(1.5, Peaks[i] * (center - 16)) : 1.5;
            dc.DrawLine(px >= X(Start) && px <= X(End) ? active : muted, new Point(px, center - amplitude), new Point(px, center + amplitude));
        }
        foreach (var value in new[] { Start, End })
        {
            var x = Math.Clamp(X(value), 3, w - 3); dc.DrawLine(active, new Point(x, 8), new Point(x, h - 30));
            dc.DrawRoundedRectangle(active.Brush, null, new Rect(x - 3, center - 15, 6, 30), 3, 3);
        }
        var play = X(Position); dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(play, 4), new Point(play, h - 30));
        for (int i = 0; i <= 4; i++)
        {
            var label = new FormattedText(TimeText.Format(Duration * i / 4), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Variable"), 11, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new Point(Math.Clamp(w * i / 4 - label.Width / 2, 8, Math.Max(8, w - label.Width - 8)), h - 24));
        }
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); Focus(); var x = e.GetPosition(this).X;
        drag = Math.Abs(x - Start / Duration * ActualWidth) < 10 ? 1 : Math.Abs(x - End / Duration * ActualWidth) < 10 ? 2 : 3;
        CaptureMouse(); Move(x); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (IsMouseCaptured) Move(e.GetPosition(this).X); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); ReleaseMouseCapture(); drag = 0; }
    private void Move(double x)
    {
        var value = Math.Round(Math.Clamp(x / ActualWidth, 0, 1) * Duration, 3);
        if (drag == 1) Start = Math.Clamp(value, 0, Math.Max(0, End - .001));
        if (drag == 2) End = Math.Clamp(value, Math.Min(Duration, Start + .001), Duration);
        if (drag == 3) { Position = value; SeekRequested?.Invoke(value); } else RangeChanged?.Invoke();
        InvalidateVisual();
    }
}
