using System.Windows.Input;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Graphics;

namespace GLORIOUSSYSTEM.App;

public partial class ReportsPage : ContentPage
{
    private bool _isRefreshing;
    private bool _isLoading;

    private readonly PhChartDrawable _chartDrawable;

    public ReportsPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Native MAUI GraphicsView chart
        _chartDrawable = new PhChartDrawable();
        ReadingsChart.Drawable = _chartDrawable;

        // Default: 24 Hours
        ChartTimeRangePicker.SelectedIndex = 2;
        ChartTimeRangePicker.SelectedIndexChanged += OnTimeRangeChanged;

    }


    // ============================================================
    // Refresh
    // ============================================================

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value)
                return;

            _isRefreshing = value;
            OnPropertyChanged();
        }
    }


    public ICommand RefreshCommand =>
        new Command(async () => await RefreshAsync());


    private async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;

        try
        {
            await LoadAsync();
        }
        finally { IsRefreshing = false; }
    }


    // ============================================================
    // Main Loading
    // ============================================================

    private async Task LoadAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;

        LogToFile("=== ReportsPage Load STARTED ===");

        try
        {
            var result = await Task.Run(() =>
            {
                using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<HydroponicDbContext>();

                var sensors = db.Sensors
                    .AsNoTracking()
                    .ToList();

                var readingsWithData = db.Readings
                    .AsNoTracking()
                    .Select(r => r.SensorId)
                    .Distinct()
                    .Count();

                var alertCount = db.Sensors
                    .AsNoTracking()
                    .Where(s =>
                        s.MinThreshold.HasValue ||
                        s.MaxThreshold.HasValue)
                    .Select(s => new
                    {
                        Sensor = s,
                        Latest = s.Readings
                            .OrderByDescending(r => r.Timestamp)
                            .FirstOrDefault()
                    })
                    .Where(x =>
                        x.Latest != null &&
                        (
                            (
                                x.Sensor.MinThreshold.HasValue &&
                                x.Latest.Value <
                                x.Sensor.MinThreshold.Value
                            )
                            ||
                            (
                                x.Sensor.MaxThreshold.HasValue &&
                                x.Latest.Value >
                                x.Sensor.MaxThreshold.Value
                            )
                        ))
                    .Count();

                return new ReportSummary
                {
                    TotalSensors = sensors.Count,
                    OnlineSensors = readingsWithData,
                    Alerts = alertCount
                };
            });


            LogToFile(
                $"=== ReportsPage Load: sensors={result.TotalSensors}, " +
                $"readingsWithData={result.OnlineSensors}, " +
                $"alerts={result.Alerts} ===");


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TotalSensorsLabel.Text =
                    result.TotalSensors.ToString();

                OnlineSensorsLabel.Text =
                    result.OnlineSensors.ToString();

                AlertCountLabel.Text =
                    result.Alerts.ToString();

            });


            await LoadChartAsync();

            LogToFile("=== ReportsPage Load COMPLETED ===");
        }
        catch (Exception ex)
        {
            LogToFile(
                $"!!! ReportsPage Load FAILED: {ex}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync(
                    "Error",
                    $"Failed to load reports:\n\n{ex.Message}",
                    "OK");
            });
        }
        finally
        {
            _isLoading = false;
        }
    }


    // ============================================================
    // Chart Time Range
    // ============================================================

    private void OnTimeRangeChanged(
        object? sender,
        EventArgs e)
    {
        _ = LoadChartAsync();
    }


    private TimeSpan GetSelectedTimeRange()
    {
        return ChartTimeRangePicker.SelectedIndex switch
        {
            0 => TimeSpan.FromHours(1),
            1 => TimeSpan.FromHours(6),
            2 => TimeSpan.FromHours(24),
            3 => TimeSpan.FromDays(7),
            _ => TimeSpan.FromHours(24)
        };
    }


    // ============================================================
    // Load pH Chart Data
    // ============================================================

    private async Task LoadChartAsync()
    {
        LogToFile("=== ReportsPage LoadChart STARTED ===");

        try
        {
            var timeRange =
                GetSelectedTimeRange();

            var cutoff =
                DateTime.UtcNow - timeRange;


            var readings = await Task.Run(() =>
            {
                using var scope =
                    (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();

                var db =
                    scope.ServiceProvider
                        .GetRequiredService<HydroponicDbContext>();

                return db.Readings
                    .AsNoTracking()
                    .Where(r =>
                        r.Metric == "pH" &&
                        r.Timestamp >= cutoff)
                    .OrderBy(r => r.Timestamp)
                    .ToList();
            });


            LogToFile(
                $"=== ReportsPage LoadChart: " +
                $"found {readings.Count} pH readings ===");


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateChart(
                    readings,
                    timeRange);
            });


            LogToFile(
                "=== ReportsPage LoadChart COMPLETED ===");
        }
        catch (Exception ex)
        {
            LogToFile(
                $"!!! ReportsPage LoadChart FAILED: {ex}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync(
                    "Chart Error",
                    $"Failed to load pH history:\n\n{ex.Message}",
                    "OK");
            });
        }
    }


    // ============================================================
    // Update Chart
    // ============================================================

    private void UpdateChart(
        List<Reading> readings,
        TimeSpan timeRange)
    {
        var points =
            readings
                .Select(r => new PhChartPoint
                {
                    Timestamp = r.Timestamp,
                    Value = r.Value
                })
                .OrderBy(p => p.Timestamp)
                .ToList();


        _chartDrawable.SetData(
            points,
            timeRange);


        // Force GraphicsView to redraw
        ReadingsChart.Invalidate();


        // --------------------------------------------------------
        // Statistics
        // --------------------------------------------------------

        if (readings.Count == 0)
        {
            ChartStats.IsVisible = false;

            CurrentValueLabel.Text = "--";
            MinValueLabel.Text = "--";
            MaxValueLabel.Text = "--";
        }
        else
        {
            var values =
                readings
                    .Select(r => r.Value)
                    .ToList();


            CurrentValueLabel.Text =
                values.Last().ToString("F1");

            MinValueLabel.Text =
                values.Min().ToString("F1");

            MaxValueLabel.Text =
                values.Max().ToString("F1");

            ChartStats.IsVisible = true;
        }


        UpdateLegend(readings);
    }


    // ============================================================
    // Chart Legend
    // ============================================================

    private void UpdateLegend(
        List<Reading> readings)
    {
        ChartLegend.Children.Clear();


        if (readings.Count == 0)
        {
            ChartLegend.Children.Add(
                new Label
                {
                    Text = "No pH data available",
                    FontSize = 12,
                    TextColor =
                        GetResourceColor(
                            "OnSurfaceVariant",
                            Colors.Gray)
                });

            return;
        }


        var earliest =
            readings.First();

        var latest =
            readings.Last();


        var change =
            latest.Value -
            earliest.Value;


        var changeColor =
            change >= 0
                ? GetResourceColor(
                    "StatusOnline",
                    Colors.Green)
                : GetResourceColor(
                    "StatusCritical",
                    Colors.Red);


        var changeIcon = change >= 0 ? "+" : "-";


        ChartLegend.Children.Add(
            new Label
            {
                Text =
                    $"Range: {earliest.Value:F1} to {latest.Value:F1}",

                FontSize = 12,

                TextColor =
                    GetResourceColor(
                        "OnSurfaceVariant",
                        Colors.Gray)
            });


        ChartLegend.Children.Add(
            new Label
            {
                Text =
                    $"{changeIcon} {Math.Abs(change):F1} pH",

                FontSize = 11,

                TextColor = changeColor,

                FontAttributes =
                    FontAttributes.Bold
            });
    }


    // ============================================================
    // API Test
    // ============================================================

    private async void OnTestApiClicked(
        object sender,
        EventArgs e)
    {
        TestApiButton.IsEnabled = false;

        ApiTestLoading.IsVisible = true;
        ApiTestLoading.IsRunning = true;

        ApiResultCard.IsVisible = false;
        ApiErrorLabel.IsVisible = false;

        ApiStatusLabel.Text = "Testing...";


        var successColor =
            GetResourceColor(
                "PrimaryContainer",
                Color.FromArgb("#16382B"));


        var errorColor =
            GetResourceColor(
                "ErrorContainer",
                Color.FromArgb("#3C1C25"));


        try
        {
            var api =
                new ApiSensorService();


            var stopwatch =
                System.Diagnostics.Stopwatch
                    .StartNew();


            var readings =
                await api.GetLatestAsync();


            stopwatch.Stop();


            var withData =
                readings.Count(
                    r => r.LatestValue.HasValue);


            ApiResultCard.BackgroundColor =
                successColor;


            ApiStatusIconLabel.Text =
                "OK";

            ApiStatusIconLabel.TextColor =
                GetResourceColor("Primary", Color.FromArgb("#5EE0A0"));


            ApiStatusLabel.Text =
                "Connected";


            ApiDetailLabel.Text =
                $"{readings.Count} sensors, " +
                $"{withData} with live data";


            ApiLatencyLabel.Text =
                $"{stopwatch.ElapsedMilliseconds} ms";


            ApiErrorLabel.IsVisible =
                false;


            ApiResultCard.IsVisible =
                true;


            ApiResultCard.Opacity = 0;
            ApiResultCard.TranslationY = 10;


            await Task.WhenAll(
                ApiResultCard.FadeToAsync(
                    1,
                    300,
                    Easing.CubicOut),

                ApiResultCard.TranslateToAsync(
                    0,
                    0,
                    300,
                    Easing.CubicOut));
        }
        catch (Exception ex)
        {
            ApiResultCard.BackgroundColor =
                errorColor;


            ApiStatusIconLabel.Text =
                "ERR";

            ApiStatusIconLabel.TextColor =
                GetResourceColor("Error", Color.FromArgb("#FF7185"));


            ApiStatusLabel.Text =
                "Connection Failed";


            ApiDetailLabel.Text =
                ex.Message;


            ApiLatencyLabel.Text =
                "-- ms";


            ApiErrorLabel.Text =
                $"Error: {ex.Message}";


            ApiErrorLabel.IsVisible =
                true;


            ApiResultCard.IsVisible =
                true;
        }
        finally
        {
            TestApiButton.IsEnabled =
                true;

            ApiTestLoading.IsVisible =
                false;

            ApiTestLoading.IsRunning =
                false;
        }
    }


    // ============================================================
    // Quick Actions
    // ============================================================

    private async void OnExportDataClicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "Export Data",
            "Data export functionality coming soon!",
            "OK");
    }


    private async void OnViewLogsClicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "View Logs",
            "System logs viewer coming soon!",
            "OK");
    }


    private async void OnCalibrateClicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "Calibrate Sensors",
            "Sensor calibration wizard coming soon!",
            "OK");
    }


    private async void OnSystemInfoClicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "System Info",
            "GLORIOUS SYSTEM v1.0\n" +
            ".NET 10 MAUI App\n" +
            "SQLite + EF Core\n" +
            "ONNX Runtime CNN",
            "OK");
    }


    // ============================================================
    // Page Lifecycle
    // ============================================================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Yield();
        await LoadAsync();
    }


    // ============================================================
    // Resource Helpers
    // ============================================================

    private static Color GetResourceColor(
        string key,
        Color fallback)
    {
        if (Application.Current?.Resources
            .TryGetValue(key, out var value) == true)
        {
            if (value is Color color)
                return color;
        }

        return fallback;
    }


    // ============================================================
    // Logging
    // ============================================================

    private static void LogToFile(
        string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }
}


// ====================================================================
// Report Summary
// ====================================================================

internal sealed class ReportSummary
{
    public int TotalSensors { get; set; }

    public int OnlineSensors { get; set; }

    public int Alerts { get; set; }
}


// ====================================================================
// pH Chart Point
// ====================================================================

internal sealed class PhChartPoint
{
    public DateTime Timestamp { get; set; }

    public double Value { get; set; }
}
