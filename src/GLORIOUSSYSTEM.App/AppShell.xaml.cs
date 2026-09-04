using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public partial class AppShell : Shell
{
    private static void LogToFile(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "startup_log.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    public AppShell()
    {
        LogToFile("=== AppShell constructor STARTED ===");
        InitializeComponent();
        LoadFlyoutData();
    }

    async void LoadFlyoutData()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = await db.Sensors.ToListAsync();
            var readingsWithData = await db.Readings.Select(r => r.SensorId).Distinct().CountAsync();
            var alertCount = await db.Sensors
                .Where(s => s.MinThreshold.HasValue || s.MaxThreshold.HasValue)
                .Select(s => new { Sensor = s, Latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault() })
                .Where(x => x.Latest != null && ((x.Sensor.MinThreshold.HasValue && x.Latest.Value < x.Sensor.MinThreshold.Value) || (x.Sensor.MaxThreshold.HasValue && x.Latest.Value > x.Sensor.MaxThreshold.Value)))
                .CountAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                FlyoutLastSync.Text = $"Last sync: {DateTime.Now:HH:mm:ss}  •  {readingsWithData}/{sensors.Count} sensors active";
                FlyoutVersion.Text = "GLORIOUSSYTEM  •  v1.0.0";
                Shell.Current.FlyoutBackgroundColor = alertCount > 0
                    ? (Color)Application.Current!.Resources["SurfaceBright"]
                    : (Color)Application.Current!.Resources["SurfaceBright"];
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load flyout data: {ex.Message}");
        }
    }

    async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Sign Out", "Are you sure you want to sign out?", "Yes", "Cancel");
        if (confirm) App.Logout();
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        LoadFlyoutData();
    }
}