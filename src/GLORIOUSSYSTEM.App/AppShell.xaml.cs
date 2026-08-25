using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(logPath, $"[{timestamp}] {message}\n");
        }
        catch { }
    }

    public AppShell()
    {
        LogToFile("=== AppShell constructor STARTED ===");
        System.Diagnostics.Debug.WriteLine("=== AppShell constructor STARTED ===");
        try
        {
            InitializeComponent();
            LogToFile("=== AppShell InitializeComponent COMPLETED ===");
            System.Diagnostics.Debug.WriteLine("=== AppShell InitializeComponent COMPLETED ===");
            LoadFlyoutData();
            LogToFile("=== AppShell LoadFlyoutData CALLED ===");
            System.Diagnostics.Debug.WriteLine("=== AppShell LoadFlyoutData CALLED ===");
        }
        catch (Exception ex)
        {
            LogToFile($"!!! AppShell constructor FAILED: {ex}");
            System.Diagnostics.Debug.WriteLine($"!!! AppShell constructor FAILED: {ex}");
            throw;
        }
        LogToFile("=== AppShell constructor COMPLETED ===");
        System.Diagnostics.Debug.WriteLine("=== AppShell constructor COMPLETED ===");
    }

    async void LoadFlyoutData()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            var sensors = await db.Sensors.ToListAsync();
            var readingsWithData = await db.Readings.Select(r => r.SensorId).Distinct().CountAsync();
            var offlineCount = sensors.Count - readingsWithData;

            var alertCount = await db.Sensors
                .Where(s => s.MinThreshold.HasValue || s.MaxThreshold.HasValue)
                .Select(s => new { Sensor = s, Latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault() })
                .Where(x => x.Latest != null &&
                    ((x.Sensor.MinThreshold.HasValue && x.Latest.Value < x.Sensor.MinThreshold.Value) ||
                     (x.Sensor.MaxThreshold.HasValue && x.Latest.Value > x.Sensor.MaxThreshold.Value)))
                .CountAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                FlyoutTotalSensors.Text = sensors.Count.ToString();
                FlyoutOnlineSensors.Text = readingsWithData.ToString();
                FlyoutAlerts.Text = alertCount.ToString();
                FlyoutVersion.Text = "v1.0.0";
                FlyoutLastSync.Text = $"Last sync: {DateTime.Now:HH:mm:ss}";
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
        if (confirm)
        {
            App.Logout();
        }
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        LogToFile($"=== AppShell OnNavigated: {args.Current?.Location?.OriginalString ?? "null"} ===");
        System.Diagnostics.Debug.WriteLine($"=== AppShell OnNavigated: {args.Current?.Location?.OriginalString ?? "null"} ===");
        base.OnNavigated(args);

        // Refresh flyout data on navigation
        LoadFlyoutData();
        LogToFile("=== AppShell OnNavigated LoadFlyoutData CALLED ===");
        System.Diagnostics.Debug.WriteLine("=== AppShell OnNavigated LoadFlyoutData CALLED ===");
    }
}