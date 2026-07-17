using GLORIOUSSYSTEM.Data.Models;

namespace GLORIOUSSYSTEM.App;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        LoadSensors();
    }

    private void LoadSensors()
    {
        using var db = new HydroponicDbContext();
        SensorList.ItemsSource = db.Sensors.ToList();
    }
}