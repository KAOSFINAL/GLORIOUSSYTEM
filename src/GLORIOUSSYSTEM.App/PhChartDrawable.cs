using Microsoft.Maui.Graphics;

namespace GLORIOUSSYSTEM.App;

internal sealed class PhChartDrawable : IDrawable
{
    private readonly object _lock = new();

    private List<PhChartPoint> _points = new();
    private TimeSpan _timeRange = TimeSpan.FromHours(24);

    public void SetData(List<PhChartPoint> points, TimeSpan timeRange)
    {
        lock (_lock)
        {
            _points = points
                .OrderBy(p => p.Timestamp)
                .ToList();

            _timeRange = timeRange;
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        List<PhChartPoint> points;
        TimeSpan timeRange;

        lock (_lock)
        {
            points = _points.ToList();
            timeRange = _timeRange;
        }

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var background = isDark
            ? Color.FromArgb("#1C1C1E")
            : Color.FromArgb("#FAFAFA");

        var textColor = isDark
            ? Color.FromArgb("#D1D1D6")
            : Color.FromArgb("#636366");

        var gridColor = isDark
            ? Color.FromArgb("#38383A")
            : Color.FromArgb("#E5E5EA");

        var lineColor = Color.FromArgb("#10B981");

        canvas.FillColor = background;
        canvas.FillRectangle(dirtyRect);

        if (points.Count == 0)
        {
            canvas.FontColor = textColor;
            canvas.FontSize = 14;
            canvas.DrawString(
                "No pH data available",
                dirtyRect,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
            return;
        }

        const float left = 48f;
        const float right = 16f;
        const float top = 20f;
        const float bottom = 38f;

        var chartWidth = dirtyRect.Width - left - right;
        var chartHeight = dirtyRect.Height - top - bottom;

        if (chartWidth <= 0 || chartHeight <= 0)
            return;

        var minValue = points.Min(p => p.Value);
        var maxValue = points.Max(p => p.Value);
        var valueRange = maxValue - minValue;

        if (valueRange < 0.1)
        {
            minValue -= 0.5;
            maxValue += 0.5;
        }
        else
        {
            var padding = Math.Max(valueRange * 0.15, 0.2);
            minValue -= padding;
            maxValue += padding;
        }

        var finalValueRange = maxValue - minValue;

        // Horizontal grid lines and Y-axis labels.
        canvas.StrokeColor = gridColor;
        canvas.StrokeSize = 1;
        canvas.FontColor = textColor;
        canvas.FontSize = 10;

        const int gridLines = 4;

        for (var i = 0; i <= gridLines; i++)
        {
            var y = top + chartHeight * i / gridLines;

            canvas.DrawLine(
                left,
                y,
                left + chartWidth,
                y);

            var value = maxValue - finalValueRange * i / gridLines;

            canvas.DrawString(
                value.ToString("F1"),
                0,
                y - 9,
                left - 7,
                18,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        // Use the selected time range for the X-axis so the chart remains
        // consistent when switching between 1h, 6h, 24h and 7d.
        var endTime = points.Last().Timestamp;
        var startTime = endTime - timeRange;
        var totalSeconds = Math.Max(timeRange.TotalSeconds, 1d);

        var screenPoints = new List<PointF>(points.Count);

        foreach (var point in points)
        {
            var elapsed = (point.Timestamp - startTime).TotalSeconds;
            var xRatio = (float)Math.Clamp(elapsed / totalSeconds, 0d, 1d);
            var yRatio = (float)Math.Clamp(
                (point.Value - minValue) / finalValueRange,
                0d,
                1d);

            var x = left + chartWidth * xRatio;
            var y = top + chartHeight * (1f - yRatio);

            screenPoints.Add(new PointF(x, y));
        }

        // Draw the pH line.
        canvas.StrokeColor = lineColor;
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        for (var i = 1; i < screenPoints.Count; i++)
        {
            canvas.DrawLine(
                screenPoints[i - 1],
                screenPoints[i]);
        }

        // Draw data points.
        canvas.FillColor = lineColor;

        foreach (var point in screenPoints)
        {
            canvas.FillCircle(point.X, point.Y, 4);
        }

        // X-axis labels.
        canvas.FontColor = textColor;
        canvas.FontSize = 10;

        var firstLabel = FormatTimestamp(startTime, timeRange);
        var lastLabel = FormatTimestamp(endTime, timeRange);

        canvas.DrawString(
            firstLabel,
            left,
            top + chartHeight + 8,
            chartWidth / 2,
            20,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        canvas.DrawString(
            lastLabel,
            left + chartWidth / 2,
            top + chartHeight + 8,
            chartWidth / 2,
            20,
            HorizontalAlignment.Right,
            VerticalAlignment.Center);
    }

    private static string FormatTimestamp(
        DateTime timestamp,
        TimeSpan timeRange)
    {
        var local = timestamp.ToLocalTime();

        if (timeRange <= TimeSpan.FromDays(1))
            return local.ToString("HH:mm");

        return local.ToString("MM/dd");
    }
}