using Microsoft.Maui.Graphics;

namespace GLORIOUSSYSTEM.App;

internal sealed class PhChartDrawable : IDrawable
{
    private readonly object _lock = new();

    private List<PhChartPoint> _points = new();

    private TimeSpan _timeRange = TimeSpan.FromHours(24);

    public void SetData(
        List<PhChartPoint> points,
        TimeSpan timeRange)
    {
        lock (_lock)
        {
            _points = points
                .OrderBy(p => p.Timestamp)
                .ToList();

            _timeRange = timeRange;
        }
    }

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        // your existing Draw() code here
    }

    private string FormatTimestamp(DateTime timestamp)
    {
        if (_timeRange <= TimeSpan.FromHours(6))
            return timestamp.ToLocalTime().ToString("HH:mm");

        if (_timeRange <= TimeSpan.FromDays(1))
            return timestamp.ToLocalTime().ToString("HH:mm");

        return timestamp.ToLocalTime().ToString("MM/dd");
    }
}