using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.App;

public class SensorDisplayItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string LatestValueText { get; set; } = "";
}

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        LoadSensors();
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        LoadSensors();
    }

    private void LoadSensors()
    {
        using var db = new HydroponicDbContext();

        var sensors = db.Sensors
            .Include(s => s.Readings)
            .ToList();

        var items = sensors.Select(s =>
        {
            var latest = s.Readings
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefault();

            return new SensorDisplayItem
            {
                Name = s.Name,
                Type = s.Type,
                LatestValueText = latest != null
                    ? $"{latest.Value} {latest.Metric} — {DateTime.Parse(latest.Timestamp):g}"
                    : "No data yet"
            };
        }).ToList();

        SensorList.ItemsSource = items;
    }
}