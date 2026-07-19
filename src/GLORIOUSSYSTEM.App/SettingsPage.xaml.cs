using GLORIOUSSYSTEM.Data.Models;

namespace GLORIOUSSYSTEM.App;

public class SensorSetting
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public bool Enabled { get; set; }
    public string? MinThreshold { get; set; }
    public string? MaxThreshold { get; set; }
}

public partial class SettingsPage : ContentPage
{
    List<SensorSetting> _items = new();

    public SettingsPage()
    {
        InitializeComponent();
        Load();
    }

    void Load()
    {
        using var db = new HydroponicDbContext();
        _items = db.Sensors.Select(s => new SensorSetting
        {
            Id = s.Id,
            Name = s.Name,
            Model = s.Model ?? s.Type,
            Enabled = s.Enabled == 1,
            MinThreshold = s.MinThreshold.ToString(),
            MaxThreshold = s.MaxThreshold.ToString()
        }).ToList();
        SettingsList.ItemsSource = _items;
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        using var db = new HydroponicDbContext();
        foreach (var item in _items)
        {
            var sensor = db.Sensors.Find(item.Id);
            if (sensor == null) continue;

            sensor.Name = item.Name;
            sensor.Enabled = item.Enabled ? 1 : 0;
            sensor.MinThreshold = double.TryParse(item.MinThreshold, out var min) ? min : null;
            sensor.MaxThreshold = double.TryParse(item.MaxThreshold, out var max) ? max : null;
        }
        db.SaveChanges();
        await DisplayAlert("Saved", "Sensor settings updated.", "OK");
    }
}