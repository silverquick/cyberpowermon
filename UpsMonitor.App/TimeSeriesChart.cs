using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace UpsMonitor.App;

public sealed class TimeSeriesChart : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(HistoryChartData),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit),
        typeof(string),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata("No data", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FixedMinimumProperty = DependencyProperty.Register(
        nameof(FixedMinimum),
        typeof(double),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FixedMaximumProperty = DependencyProperty.Register(
        nameof(FixedMaximum),
        typeof(double),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact),
        typeof(bool),
        typeof(TimeSeriesChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LeftMargin = 58;
    private const double RightMargin = 16;
    private const double TopMargin = 36;
    private const double BottomMargin = 30;
    private double? _cursorX;

    public TimeSeriesChart()
    {
        SnapsToDevicePixels = true;
        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;
    }

    public HistoryChartData? Data
    {
        get => (HistoryChartData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public double FixedMinimum
    {
        get => (double)GetValue(FixedMinimumProperty);
        set => SetValue(FixedMinimumProperty, value);
    }

    public double FixedMaximum
    {
        get => (double)GetValue(FixedMaximumProperty);
        set => SetValue(FixedMaximumProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var plot = Compact
            ? new Rect(0, 1, Math.Max(0, ActualWidth), Math.Max(0, ActualHeight - 2))
            : new Rect(
                LeftMargin,
                TopMargin,
                Math.Max(0, ActualWidth - LeftMargin - RightMargin),
                Math.Max(0, ActualHeight - TopMargin - BottomMargin));
        if (plot.Width < 20 || plot.Height < 20)
        {
            return;
        }

        var textBrush = ResourceBrush("TextBrush", Brushes.White);
        var mutedBrush = ResourceBrush("MutedTextBrush", Brushes.Gray);
        var borderBrush = ResourceBrush("BorderBrush", Brushes.DimGray);
        if (!Compact)
        {
            drawingContext.DrawRectangle(ResourceBrush("SecondaryPanelBrush", Brushes.Transparent), null, plot);
        }

        var data = Data;
        var points = data?.Series.SelectMany(series => series.Points).ToArray() ?? [];
        if (data is null || data.To <= data.From || points.Length == 0)
        {
            if (!Compact)
            {
                DrawText(drawingContext, EmptyText, plot.Left + 12, plot.Top + 12, 12, mutedBrush);
                DrawAxes(drawingContext, plot, borderBrush);
            }

            return;
        }

        var referenceValues = Compact
            ? Enumerable.Empty<double>()
            : data.ReferenceLines.Select(line => line.Value);
        var minimum = double.IsNaN(FixedMinimum)
            ? points.Select(point => point.Minimum).Concat(referenceValues).Min()
            : FixedMinimum;
        var maximum = double.IsNaN(FixedMaximum)
            ? points.Select(point => point.Maximum).Concat(referenceValues).Max()
            : FixedMaximum;
        if (double.IsNaN(FixedMinimum) || double.IsNaN(FixedMaximum))
        {
            ExpandRange(ref minimum, ref maximum);
            if (!double.IsNaN(FixedMinimum))
            {
                minimum = FixedMinimum;
            }

            if (!double.IsNaN(FixedMaximum))
            {
                maximum = FixedMaximum;
            }
        }

        if (!Compact)
        {
            DrawGrid(drawingContext, plot, minimum, maximum, data.From, data.To, mutedBrush, borderBrush);
        }

        if (!Compact)
        {
            foreach (var reference in data.ReferenceLines)
            {
                var y = ValueToY(reference.Value, minimum, maximum, plot);
                var pen = new Pen(ParseBrush(reference.Color), 1) { DashStyle = DashStyles.Dash };
                drawingContext.DrawLine(pen, new(plot.Left, y), new(plot.Right, y));
            }

            foreach (var marker in data.Events)
            {
                if (marker.Timestamp < data.From || marker.Timestamp > data.To)
                {
                    continue;
                }

                var x = TimeToX(marker.Timestamp, data.From, data.To, plot);
                var pen = new Pen(ParseBrush(marker.Color), 1) { DashStyle = DashStyles.Dot };
                drawingContext.DrawLine(pen, new(x, plot.Top), new(x, plot.Bottom));
                drawingContext.DrawGeometry(
                    ParseBrush(marker.Color),
                    null,
                    Triangle(new(x, plot.Top), 4));
            }
        }

        foreach (var series in data.Series)
        {
            DrawSeries(drawingContext, series, data.From, data.To, minimum, maximum, plot);
        }

        if (!Compact)
        {
            DrawLegend(drawingContext, data, textBrush);
        }

        if (!Compact && _cursorX is { } cursorX && cursorX >= plot.Left && cursorX <= plot.Right)
        {
            var cursorPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 148, 163, 184)), 1)
            {
                DashStyle = DashStyles.Dash,
            };
            drawingContext.DrawLine(cursorPen, new(cursorX, plot.Top), new(cursorX, plot.Bottom));
            DrawHoverTooltip(drawingContext, data, cursorX, plot, textBrush, mutedBrush, borderBrush);
        }
    }

    private void DrawSeries(
        DrawingContext drawingContext,
        HistoryChartSeries series,
        DateTimeOffset from,
        DateTimeOffset to,
        double minimum,
        double maximum,
        Rect plot)
    {
        if (series.Points.Count == 0)
        {
            return;
        }

        var brush = ParseBrush(series.Color);
        var rangePen = new Pen(WithOpacity(brush, 0.24), 1);
        foreach (var point in series.Points)
        {
            var x = TimeToX(point.Timestamp, from, to, plot);
            drawingContext.DrawLine(
                rangePen,
                new(x, ValueToY(point.Minimum, minimum, maximum, plot)),
                new(x, ValueToY(point.Maximum, minimum, maximum, plot)));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var started = false;
            DateTimeOffset? previousTimestamp = null;
            var expectedSpacing = TimeSpan.FromTicks(Math.Max(1, (to - from).Ticks / Math.Max(1, series.Points.Count)));
            var gapLimit = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(15).Ticks, expectedSpacing.Ticks * 6));
            foreach (var point in series.Points)
            {
                var chartPoint = new Point(
                    TimeToX(point.Timestamp, from, to, plot),
                    ValueToY(point.Average, minimum, maximum, plot));
                if (!started || previousTimestamp is { } previous && point.Timestamp - previous > gapLimit)
                {
                    context.BeginFigure(chartPoint, false, false);
                    started = true;
                }
                else
                {
                    context.LineTo(chartPoint, true, false);
                }

                previousTimestamp = point.Timestamp;
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(brush, 1.8), geometry);

        if (series.Points.Count == 1)
        {
            var point = series.Points[0];
            drawingContext.DrawEllipse(
                brush,
                null,
                new(
                    TimeToX(point.Timestamp, from, to, plot),
                    ValueToY(point.Average, minimum, maximum, plot)),
                2.5,
                2.5);
        }
    }

    private void DrawGrid(
        DrawingContext drawingContext,
        Rect plot,
        double minimum,
        double maximum,
        DateTimeOffset from,
        DateTimeOffset to,
        Brush mutedBrush,
        Brush borderBrush)
    {
        var gridPen = new Pen(WithOpacity(borderBrush, 0.58), 1);
        for (var index = 0; index <= 4; index++)
        {
            var ratio = index / 4d;
            var y = plot.Bottom - (plot.Height * ratio);
            drawingContext.DrawLine(gridPen, new(plot.Left, y), new(plot.Right, y));
            var value = minimum + ((maximum - minimum) * ratio);
            DrawText(drawingContext, FormatValue(value), 4, y - 8, 10, mutedBrush);
        }

        for (var index = 0; index <= 4; index++)
        {
            var ratio = index / 4d;
            var x = plot.Left + (plot.Width * ratio);
            drawingContext.DrawLine(gridPen, new(x, plot.Top), new(x, plot.Bottom));
            var timestamp = from + TimeSpan.FromTicks((long)((to - from).Ticks * ratio));
            var label = to - from >= TimeSpan.FromDays(2)
                ? timestamp.LocalDateTime.ToString("M/d HH:mm", CultureInfo.CurrentCulture)
                : timestamp.LocalDateTime.ToString("HH:mm", CultureInfo.CurrentCulture);
            var text = CreateText(label, 10, mutedBrush);
            drawingContext.DrawText(text, new(Math.Clamp(x - (text.Width / 2), plot.Left, plot.Right - text.Width), plot.Bottom + 7));
        }

        DrawAxes(drawingContext, plot, borderBrush);
    }

    private void DrawLegend(DrawingContext drawingContext, HistoryChartData data, Brush textBrush)
    {
        var x = LeftMargin;
        foreach (var series in data.Series)
        {
            drawingContext.DrawRectangle(ParseBrush(series.Color), null, new(x, 13, 10, 3));
            x += 15;
            var text = CreateText(series.DisplayName, 11, textBrush);
            drawingContext.DrawText(text, new(x, 7));
            x += text.Width + 18;
        }
    }

    private void DrawHoverTooltip(
        DrawingContext drawingContext,
        HistoryChartData data,
        double cursorX,
        Rect plot,
        Brush textBrush,
        Brush mutedBrush,
        Brush borderBrush)
    {
        var ratio = Math.Clamp((cursorX - plot.Left) / plot.Width, 0.0, 1.0);
        var timestamp = data.From + TimeSpan.FromTicks((long)((data.To - data.From).Ticks * ratio));
        var timeLabel = timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        var lines = new List<(Brush Color, string Text)>();
        foreach (var series in data.Series)
        {
            var point = series.Points.MinBy(item => Math.Abs((item.Timestamp - timestamp).Ticks));
            if (point is not null)
            {
                var valueStr = $"{point.Average:0.##}{(string.IsNullOrEmpty(Unit) ? string.Empty : $" {Unit}")}";
                lines.Add((ParseBrush(series.Color), $"{series.DisplayName}: {valueStr}"));
            }
        }

        if (lines.Count == 0)
        {
            return;
        }

        var headerText = CreateText(timeLabel, 10.5, mutedBrush);
        var itemTexts = lines.Select(l => (l.Color, Text: CreateText(l.Text, 11, textBrush))).ToList();

        var contentWidth = Math.Max(headerText.Width, itemTexts.Max(it => it.Text.Width + 14));
        var boxWidth = contentWidth + 20;
        var lineHeight = 16.0;
        var boxHeight = 12 + headerText.Height + 6 + (itemTexts.Count * lineHeight) + 6;

        // Position tooltip to left or right of cursor
        var boxX = cursorX + 14 + boxWidth > plot.Right
            ? cursorX - boxWidth - 14
            : cursorX + 14;
        boxX = Math.Clamp(boxX, plot.Left + 4, plot.Right - boxWidth - 4);
        var boxY = Math.Clamp(plot.Top + 10, plot.Top + 4, plot.Bottom - boxHeight - 4);

        var tooltipRect = new Rect(boxX, boxY, boxWidth, boxHeight);
        var bgBrush = ResourceBrush("PanelBrush", new SolidColorBrush(Color.FromArgb(240, 24, 28, 36)));
        var bgPen = new Pen(borderBrush, 1);

        drawingContext.DrawRoundedRectangle(bgBrush, bgPen, tooltipRect, 6, 6);

        var currentY = boxY + 8;
        drawingContext.DrawText(headerText, new(boxX + 10, currentY));
        currentY += headerText.Height + 6;

        foreach (var item in itemTexts)
        {
            drawingContext.DrawEllipse(item.Color, null, new Point(boxX + 14, currentY + 7), 3.5, 3.5);
            drawingContext.DrawText(item.Text, new(boxX + 22, currentY));
            currentY += lineHeight;
        }
    }

    private static void DrawAxes(DrawingContext drawingContext, Rect plot, Brush borderBrush)
    {
        var pen = new Pen(borderBrush, 1);
        drawingContext.DrawLine(pen, plot.BottomLeft, plot.BottomRight);
        drawingContext.DrawLine(pen, plot.TopLeft, plot.BottomLeft);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var data = Data;
        if (Compact)
        {
            return;
        }
        var plotWidth = ActualWidth - LeftMargin - RightMargin;
        var position = e.GetPosition(this);
        if (data is null || plotWidth <= 0 || position.X < LeftMargin || position.X > ActualWidth - RightMargin)
        {
            return;
        }

        _cursorX = position.X;
        InvalidateVisual();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _cursorX = null;
        InvalidateVisual();
    }

    private string FormatValue(double value) => string.IsNullOrEmpty(Unit)
        ? value.ToString("0.##", CultureInfo.CurrentCulture)
        : $"{value:0.##} {Unit}";

    private static void ExpandRange(ref double minimum, ref double maximum)
    {
        if (Math.Abs(maximum - minimum) < 0.000001)
        {
            var padding = Math.Max(1, Math.Abs(maximum) * 0.05);
            minimum -= padding;
            maximum += padding;
            return;
        }

        var range = maximum - minimum;
        minimum -= range * 0.08;
        maximum += range * 0.08;
    }

    private static double TimeToX(DateTimeOffset value, DateTimeOffset from, DateTimeOffset to, Rect plot) =>
        plot.Left + (plot.Width * ((value - from).TotalMilliseconds / (to - from).TotalMilliseconds));

    private static double ValueToY(double value, double minimum, double maximum, Rect plot) =>
        plot.Bottom - (plot.Height * ((value - minimum) / (maximum - minimum)));

    private static StreamGeometry Triangle(Point top, double size)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(top.X, top.Y), true, true);
            context.LineTo(new(top.X - size, top.Y + size * 1.6), true, false);
            context.LineTo(new(top.X + size, top.Y + size * 1.6), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private Brush ResourceBrush(string key, Brush fallback) =>
        TryFindResource(key) as Brush ?? fallback;

    private static Brush ParseBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private static Brush WithOpacity(Brush source, double opacity)
    {
        var clone = source.Clone();
        clone.Opacity = opacity;
        clone.Freeze();
        return clone;
    }

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
