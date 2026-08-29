using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed class WeeklyHeatmapControl : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(WeeklyPatternResult),
        typeof(WeeklyHeatmapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit),
        typeof(string),
        typeof(WeeklyHeatmapControl),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(WeeklyHeatmapControl),
        new FrameworkPropertyMetadata("No data", FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LeftMargin = 48;
    private const double RightMargin = 20;
    private const double TopMargin = 32;
    private const double BottomMargin = 44;
    private const double CellGap = 3.0;
    private const double CellCornerRadius = 3.0;

    private static readonly string[] DayNames = ["月", "火", "水", "木", "金", "土", "日"];
    private static readonly int[] DayOrder = [1, 2, 3, 4, 5, 6, 0]; // 1=Mon, ..., 0=Sun

    private (int Row, int Col)? _hoveredCell;
    private Point _mousePosition;

    public WeeklyHeatmapControl()
    {
        SnapsToDevicePixels = true;
        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;
    }

    public WeeklyPatternResult? Data
    {
        get => (WeeklyPatternResult?)GetValue(DataProperty);
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

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        _mousePosition = e.GetPosition(this);
        var cell = HitTestCell(_mousePosition);
        if (cell != _hoveredCell)
        {
            _hoveredCell = cell;
            InvalidateVisual();
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoveredCell != null)
        {
            _hoveredCell = null;
            InvalidateVisual();
        }
    }

    private (int Row, int Col)? HitTestCell(Point position)
    {
        var data = Data;
        if (data == null || data.TotalSamples == 0)
        {
            return null;
        }

        var availableWidth = ActualWidth - LeftMargin - RightMargin;
        var availableHeight = ActualHeight - TopMargin - BottomMargin;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return null;
        }

        var cellWidth = availableWidth / 24.0;
        var cellHeight = availableHeight / 7.0;

        var x = position.X - LeftMargin;
        var y = position.Y - TopMargin;

        if (x < 0 || x >= availableWidth || y < 0 || y >= availableHeight)
        {
            return null;
        }

        var col = (int)(x / cellWidth);
        var row = (int)(y / cellHeight);
        col = Math.Clamp(col, 0, 23);
        row = Math.Clamp(row, 0, 6);

        return (row, col);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.Transparent, null, bounds);

        var data = Data;
        if (data == null || data.TotalSamples == 0 || data.Grid.Count == 0)
        {
            RenderEmpty(dc, bounds);
            return;
        }

        var availableWidth = ActualWidth - LeftMargin - RightMargin;
        var availableHeight = ActualHeight - TopMargin - BottomMargin;
        if (availableWidth <= 50 || availableHeight <= 50)
        {
            return;
        }

        var cellWidth = availableWidth / 24.0;
        var cellHeight = availableHeight / 7.0;

        var min = data.OverallMin;
        var max = data.OverallMax;
        if (Math.Abs(max - min) < 0.0001)
        {
            max = min + 1.0;
        }

        // Draw Hour Header (00..23)
        for (var col = 0; col < 24; col++)
        {
            var headerX = LeftMargin + col * cellWidth + cellWidth / 2.0;
            var text = FormatText($"{col:D2}", 10, ResolveBrush("MutedTextBrush", "#94A3B8"), TextAlignment.Center);
            dc.DrawText(text, new Point(headerX - text.Width / 2.0, TopMargin - 20));
        }

        // Map grid points by (Row, Col)
        var gridMap = new Dictionary<(int Row, int Col), HourlyPatternPoint>();
        for (var row = 0; row < 7; row++)
        {
            var dow = DayOrder[row];
            for (var col = 0; col < 24; col++)
            {
                var point = data.Grid.FirstOrDefault(p => p.DayOfWeek == dow && p.HourOfDay == col);
                if (point != null)
                {
                    gridMap[(row, col)] = point;
                }
            }
        }

        // Draw Rows & Cells
        for (var row = 0; row < 7; row++)
        {
            var dow = DayOrder[row];
            var rowY = TopMargin + row * cellHeight;

            // Day Label
            var dayLabel = LocalizationManager.Get($"Day_{dow}") ?? DayNames[row];
            var dayText = FormatText(dayLabel, 11, ResolveBrush("TextBrush", "#E2E8F0"), TextAlignment.Right);
            dc.DrawText(dayText, new Point(LeftMargin - 12 - dayText.Width, rowY + cellHeight / 2.0 - dayText.Height / 2.0));

            for (var col = 0; col < 24; col++)
            {
                var cellX = LeftMargin + col * cellWidth;
                var cellRect = new Rect(cellX + CellGap / 2.0, rowY + CellGap / 2.0, Math.Max(2, cellWidth - CellGap), Math.Max(2, cellHeight - CellGap));

                gridMap.TryGetValue((row, col), out var pt);
                var hasData = pt != null && pt.SampleCount > 0;

                Brush fillBrush;
                if (!hasData)
                {
                    fillBrush = ResolveBrush("HeatmapEmptyCellBrush", "#1E293B");
                }
                else
                {
                    var t = Math.Clamp((pt!.Average - min) / (max - min), 0.0, 1.0);
                    fillBrush = GetHeatmapBrush(t, data.Metric);
                }

                var isHovered = _hoveredCell.HasValue && _hoveredCell.Value.Row == row && _hoveredCell.Value.Col == col;
                var pen = isHovered
                    ? new Pen(Brushes.White, 2.0)
                    : null;

                dc.DrawRoundedRectangle(fillBrush, pen, cellRect, CellCornerRadius, CellCornerRadius);

                // If cell is wide/high enough, draw numeric value inside
                if (hasData && cellWidth >= 32 && cellHeight >= 22)
                {
                    var valStr = pt!.Average >= 100 ? $"{pt.Average:0}" : $"{pt.Average:0.#}";
                    var valText = FormatText(valStr, 9, Brushes.White, TextAlignment.Center);
                    valText.SetFontWeight(FontWeights.SemiBold);
                    dc.DrawText(valText, new Point(cellRect.X + cellRect.Width / 2.0 - valText.Width / 2.0, cellRect.Y + cellRect.Height / 2.0 - valText.Height / 2.0));
                }
            }
        }

        // Draw Legend Colorbar
        RenderLegend(dc, data, LeftMargin, ActualHeight - BottomMargin + 14, availableWidth, 10);

        // Draw Tooltip if hovered
        if (_hoveredCell.HasValue && gridMap.TryGetValue(_hoveredCell.Value, out var hoverPt) && hoverPt.SampleCount > 0)
        {
            RenderTooltip(dc, hoverPt, data, _mousePosition);
        }
    }

    private void RenderLegend(DrawingContext dc, WeeklyPatternResult data, double x, double y, double width, double height)
    {
        var legendWidth = Math.Min(320, width);
        var legendX = x + (width - legendWidth) / 2.0;

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
        };
        gradient.GradientStops.Add(new GradientStop(GetHeatmapColor(0.0, data.Metric), 0.0));
        gradient.GradientStops.Add(new GradientStop(GetHeatmapColor(0.33, data.Metric), 0.33));
        gradient.GradientStops.Add(new GradientStop(GetHeatmapColor(0.66, data.Metric), 0.66));
        gradient.GradientStops.Add(new GradientStop(GetHeatmapColor(1.0, data.Metric), 1.0));

        var barRect = new Rect(legendX, y, legendWidth, height);
        dc.DrawRoundedRectangle(gradient, null, barRect, 3, 3);

        var unit = Unit;
        var minText = FormatText($"{data.OverallMin:0.#} {unit}", 10, ResolveBrush("MutedTextBrush", "#94A3B8"), TextAlignment.Left);
        var avgText = FormatText($"平均: {data.OverallAvg:0.#} {unit}", 10, ResolveBrush("MutedTextBrush", "#94A3B8"), TextAlignment.Center);
        var maxText = FormatText($"{data.OverallMax:0.#} {unit}", 10, ResolveBrush("MutedTextBrush", "#94A3B8"), TextAlignment.Right);

        dc.DrawText(minText, new Point(legendX, y + height + 2));
        dc.DrawText(avgText, new Point(legendX + legendWidth / 2.0 - avgText.Width / 2.0, y + height + 2));
        dc.DrawText(maxText, new Point(legendX + legendWidth - maxText.Width, y + height + 2));
    }

    private void RenderTooltip(DrawingContext dc, HourlyPatternPoint pt, WeeklyPatternResult data, Point mousePos)
    {
        var dowName = DayNames[Array.IndexOf(DayOrder, pt.DayOfWeek)];
        var dayFull = LocalizationManager.Get($"DayFull_{pt.DayOfWeek}") ?? $"{dowName}曜日";
        var timeTitle = $"{dayFull} {pt.HourOfDay:D2}:00 - {pt.HourOfDay + 1:D2}:00";
        var unit = Unit;

        var line1 = timeTitle;
        var line2 = $"平均: {pt.Average:0.##} {unit}";
        var line3 = $"範囲: {pt.Minimum:0.#} ～ {pt.Maximum:0.#} {unit}";
        var line4 = $"サンプル数: {pt.SampleCount:N0} 分";

        var t1 = FormatText(line1, 12, Brushes.White, TextAlignment.Left);
        t1.SetFontWeight(FontWeights.Bold);
        var t2 = FormatText(line2, 11, new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), TextAlignment.Left);
        t2.SetFontWeight(FontWeights.SemiBold);
        var t3 = FormatText(line3, 10, new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), TextAlignment.Left);
        var t4 = FormatText(line4, 9, new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), TextAlignment.Left);

        var cardWidth = Math.Max(180, Math.Max(t1.Width, Math.Max(t2.Width, t3.Width)) + 24);
        var cardHeight = t1.Height + t2.Height + t3.Height + t4.Height + 22;

        var tipX = mousePos.X + 16;
        var tipY = mousePos.Y + 16;
        if (tipX + cardWidth > ActualWidth - 10)
        {
            tipX = mousePos.X - cardWidth - 16;
        }
        if (tipY + cardHeight > ActualHeight - 10)
        {
            tipY = mousePos.Y - cardHeight - 16;
        }

        var cardRect = new Rect(tipX, tipY, cardWidth, cardHeight);
        var bgBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0x0F, 0x17, 0x2A));
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), 1.0);
        dc.DrawRoundedRectangle(bgBrush, borderPen, cardRect, 6, 6);

        var curY = tipY + 8;
        dc.DrawText(t1, new Point(tipX + 12, curY));
        curY += t1.Height + 3;
        dc.DrawText(t2, new Point(tipX + 12, curY));
        curY += t2.Height + 2;
        dc.DrawText(t3, new Point(tipX + 12, curY));
        curY += t3.Height + 2;
        dc.DrawText(t4, new Point(tipX + 12, curY));
    }

    private static Brush GetHeatmapBrush(double t, TelemetryMetric metric) =>
        new SolidColorBrush(GetHeatmapColor(t, metric));

    private static Color GetHeatmapColor(double t, TelemetryMetric metric)
    {
        t = Math.Clamp(t, 0.0, 1.0);

        // Power / Load: Slate Blue -> Teal -> Amber -> Red
        if (metric is TelemetryMetric.ActivePowerWatts or TelemetryMetric.LoadPercent)
        {
            if (t < 0.33)
            {
                var f = t / 0.33;
                return Interpolate(Color.FromRgb(0x1E, 0x29, 0x3B), Color.FromRgb(0x02, 0x84, 0xC7), f);
            }
            if (t < 0.66)
            {
                var f = (t - 0.33) / 0.33;
                return Interpolate(Color.FromRgb(0x02, 0x84, 0xC7), Color.FromRgb(0x10, 0xB9, 0x81), f);
            }
            if (t < 0.85)
            {
                var f = (t - 0.66) / 0.19;
                return Interpolate(Color.FromRgb(0x10, 0xB9, 0x81), Color.FromRgb(0xF5, 0x9E, 0x0B), f);
            }
            {
                var f = (t - 0.85) / 0.15;
                return Interpolate(Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0xEF, 0x44, 0x44), f);
            }
        }

        // Voltage: Blue (Low) -> Green (Nominal ~100V) -> Amber/Red (High)
        if (t < 0.5)
        {
            var f = t / 0.5;
            return Interpolate(Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0x10, 0xB9, 0x81), f);
        }
        else
        {
            var f = (t - 0.5) / 0.5;
            return Interpolate(Color.FromRgb(0x10, 0xB9, 0x81), Color.FromRgb(0xF9, 0x73, 0x16), f);
        }
    }

    private static Color Interpolate(Color c1, Color c2, double factor)
    {
        var r = (byte)(c1.R + (c2.R - c1.R) * factor);
        var g = (byte)(c1.G + (c2.G - c1.G) * factor);
        var b = (byte)(c1.B + (c2.B - c1.B) * factor);
        return Color.FromRgb(r, g, b);
    }

    private void RenderEmpty(DrawingContext dc, Rect bounds)
    {
        var text = FormatText(EmptyText, 14, ResolveBrush("MutedTextBrush", "#94A3B8"), TextAlignment.Center);
        dc.DrawText(text, new Point((bounds.Width - text.Width) / 2.0, (bounds.Height - text.Height) / 2.0));
    }

    private FormattedText FormatText(string text, double size, Brush brush, TextAlignment align)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Display, Segoe UI"),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = align,
        };
        return ft;
    }

    private Brush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (TryFindResource(resourceKey) is Brush brush)
        {
            return brush;
        }

        return (SolidColorBrush)new BrushConverter().ConvertFromString(fallbackHex)!;
    }
}
