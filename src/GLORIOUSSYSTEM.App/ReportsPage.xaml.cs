using GLORIOUSSYSTEM.Data.Models;
using Microcharts;
using SkiaSharp;

namespace GLORIOUSSYSTEM.App;

public partial class ReportsPage : ContentPage
{
    public ReportsPage()
    {
        InitializeComponent();
        Load();
    }

    void Load()
    {
        using var db = new HydroponicDbContext();
        var sensors = db.Sensors.Select(s => s).ToList();
        var readings = db.Readings.Where(r => r.Metric == "pH").OrderBy(r => r.Timestamp).ToList();

        var total = sensors.Count;
        var withData = db.Readings.Select(r => r.SensorId).Distinct().Count();
        var offline = total - withData;

        SummaryBar.Children.Clear();
        SummaryBar.Children.Add(new Label { Text = $"{total} sensors", TextColor = Color.FromArgb("#F1F5F9"), FontSize = 14, FontAttributes = FontAttributes.Bold });
        SummaryBar.Children.Add(new Label { Text = $"{withData} reporting", TextColor = Color.FromArgb("#22C55E"), FontSize = 14 });
        SummaryBar.Children.Add(new Label { Text = $"{offline} offline", TextColor = Color.FromArgb("#94A3B8"), FontSize = 14 });

        var entries = readings.Select(r => new ChartEntry((float)r.Value)
        {
            Label = DateTime.Parse(r.Timestamp).ToString("t"),
            ValueLabel = r.Value.ToString(),
            Color = SKColor.Parse("#22C55E")
        }).ToArray();

        ReadingsChart.Chart = new LineChart { Entries = entries, LineMode = LineMode.Straight, BackgroundColor = SKColor.Parse("#1E293B") };
    }

    async void OnTestApiClicked(object sender, EventArgs e)
    {
        ApiResultLabel.Text = "Connecting to API...";
        try
        {
            var api = new ApiSensorService();
            var readings = await api.GetLatestAsync();
            var withData = readings.Count(r => r.LatestValue.HasValue);
            ApiResultLabel.Text = $"API connected! {readings.Count} sensors returned, {withData} with live readings. " +
                                   $"First sensor: {readings.First().Name} = {readings.First().LatestValue?.ToString() ?? "no data"}";
        }
        catch (Exception ex)
        {
            ApiResultLabel.Text = $"API connection failed: {ex.Message}";
        }
    }
}