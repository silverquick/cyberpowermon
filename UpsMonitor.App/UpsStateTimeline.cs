using System.Globalization;
using System.Windows;
using System.Windows.Media;
using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed class UpsStateTimeline : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(HistoryStateTimelineData),
        typeof(UpsStateTimeline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(UpsStateTimeline),
        new FrameworkPropertyMetadata("No data", FrameworkPropertyMetadataOptions.AffectsRender));

    public HistoryStateTimelineData? Data
    {
        get => (HistoryStateTimelineData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var data = Data;
        var bar = new Rect(14, 24, Math.Max(0, ActualWidth - 28), 30);
        var mutedBrush = TryFindResource("MutedTextBrush") as Brush ?? Brushes.Gray;
        var borderBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.DimGray;
        drawingContext.DrawRoundedRectangle(
            TryFindResource("SecondaryPanelBrush") as Brush,
            new Pen(borderBrush, 1),
            bar,
            5,
            5);

        if (data is null || data.To <= data.From || data.StateChanges.Count == 0)
        {
            DrawText(drawingContext, EmptyText, bar.Left + 10, bar.Top + 7, 11, mutedBrush);
            return;
        }

        for (var index = 0; index < data.StateChanges.Count; index++)
        {
            var change = data.StateChanges[index];
            var end = index + 1 < data.StateChanges.Count ? data.StateChanges[index + 1].Timestamp : data.To;
            var start = change.Timestamp < data.From ? data.From : change.Timestamp;
            if (end <= data.From || start >= data.To)
            {
                continue;
            }

            end = end > data.To ? data.To : end;
            var x1 = TimeToX(start, data.From, data.To, bar);
            var x2 = TimeToX(end, data.From, data.To, bar);
            drawingContext.DrawRectangle(StateBrush(change.State), null, new(x1, bar.Top + 1, Math.Max(1, x2 - x1), bar.Height - 2));
        }

        foreach (var marker in data.Events)
        {
            if (marker.Timestamp < data.From || marker.Timestamp > data.To)
            {
                continue;
            }

            var x = TimeToX(marker.Timestamp, data.From, data.To, bar);
            drawingContext.DrawLine(new Pen(ParseBrush(marker.Color), 2), new(x, bar.Top - 7), new(x, bar.Bottom + 7));
        }

        DrawTimeLabel(drawingContext, data.From, bar.Left, bar.Bottom + 8, mutedBrush, false);
        DrawTimeLabel(drawingContext, data.To, bar.Right, bar.Bottom + 8, mutedBrush, true);
    }

    private void DrawTimeLabel(
        DrawingContext context,
        DateTimeOffset timestamp,
        double x,
        double y,
        Brush brush,
        bool rightAligned)
    {
        var text = CreateText(timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture), 10, brush);
        context.DrawText(text, new(rightAligned ? x - text.Width : x, y));
    }

    private static Brush StateBrush(UpsPowerState state) => state switch
    {
        UpsPowerState.Online => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        UpsPowerState.OnBattery => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        UpsPowerState.LowBattery => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
        UpsPowerState.Critical => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
    };

    private static Brush ParseBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private static double TimeToX(DateTimeOffset value, DateTimeOffset from, DateTimeOffset to, Rect bar) =>
        bar.Left + (bar.Width * ((value - from).TotalMilliseconds / (to - from).TotalMilliseconds));

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(CreateText(text, size, brush), new(x, y));

    private FormattedText CreateText(string text, double size, Brush brush) => new(
        text,
        CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        new Typeface("Segoe UI Variable Text"),
        size,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
