using System.Collections.ObjectModel;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GLORIOUSSYSTEM.App;

public class SensorDisplayItem
{
    public string Name { get; set; } = "";
    public string SubText { get; set; } = "";
    public string ValueText { get; set; } = "";
    public Color StatusColor { get; set; } = Colors.Gray;
}

public class SensorGroup : ObservableCollection<SensorDisplayItem>
{
    public string CategoryName { get; set; } = "";
    public SensorGroup(string name, IEnumerable<SensorDisplayItem> items) : base(items)
    {
        CategoryName = name;
    }
}

public partial class MainPage : ContentPage
{
    static readonly Color HasData = Color.FromArgb("#22C55E");
    static readonly Color NoData = Color.FromArgb("#475569");
    static readonly Color OutOfRange = Color.FromArgb("#F87171");

    public MainPage()
    {
        InitializeComponent();
        LoadSensors();
    }

    void OnRefreshClicked(object sender, EventArgs e) => LoadSensors();

    void LoadSensors()
    {
        using var db = new HydroponicDbContext();
        var sensors = db.Sensors.Include(s => s.Readings).ToList();

        SensorDisplayItem ToItem(Sensor s)
        {
            var latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault();
            var color = NoData;
            if (latest != null)
            {
                bool outOfRange = (s.MinThreshold.HasValue && latest.Value < s.MinThreshold) ||
                                   (s.MaxThreshold.HasValue && latest.Value > s.MaxThreshold);
                color = outOfRange ? OutOfRange : HasData;
            }
            return new SensorDisplayItem
            {
                Name = s.Name,
                SubText = s.Model ?? s.Type,
                ValueText = latest != null ? $"{latest.Value} {latest.Metric}" : "--",
                StatusColor = color
            };
        }

        var groups = new ObservableCollection<SensorGroup>
        {
            new("WATER QUALITY", sensors.Where(s => new[]{"pH","TDS","WaterTemp","UltrasonicLevel"}.Contains(s.Type)).Select(ToItem)),
            new("ENVIRONMENTAL", sensors.Where(s => s.Type is "BME280" or "BH1750").Select(ToItem)),
            new("WATER FLOW", sensors.Where(s => s.Type == "FlowRate").Select(ToItem)),
        };

        SensorList.ItemsSource = groups;

        // Summary bar
        var total = sensors.Count;
        var withData = sensors.Count(s => s.Readings.Any());
        var offline = total - withData;

        }
}