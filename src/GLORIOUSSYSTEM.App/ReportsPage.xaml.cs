using System.Windows.Input;
using System.Text;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public partial class ReportsPage : ContentPage
{
    private bool _isRefreshing;
    private bool _isLoading;
    private bool? _usingWideSummary;
    private bool _chartSelectionReady;
    private DateTime _lastLoadedUtc = DateTime.MinValue;
    private static readonly TimeSpan AutomaticRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly SensorChartDrawable _chartDrawable;

    public ReportsPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Native MAUI GraphicsView chart
        _chartDrawable = new SensorChartDrawable();
        ReadingsChart.Drawable = _chartDrawable;

        // Default: 24 Hours
        ChartTimeRangePicker.SelectedIndex = 2;
        ChartTimeRangePicker.SelectedIndexChanged += OnTimeRangeChanged;
        ChartSensorPicker.SelectedIndexChanged += OnChartSensorChanged;
        RefreshCommand = new Command(async () => await RefreshAsync());
        ThemeManager.ThemeChanged += OnThemeChanged;

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


    public ICommand RefreshCommand { get; }

    private void OnReportsSizeChanged(object? sender, EventArgs e)
    {
        if (ReportSummaryGrid.Width <= 0)
            return;

        var useWideSummary = ReportSummaryGrid.Width >= 780;
        if (_usingWideSummary == useWideSummary)
            return;

        _usingWideSummary = useWideSummary;
        ReportSummaryGrid.RowDefinitions.Clear();
        ReportSummaryGrid.ColumnDefinitions.Clear();

        if (useWideSummary)
        {
            ReportSummaryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.2, GridUnitType.Star)));
            ReportSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetRow(TotalSensorsCard, 0);
            Grid.SetColumn(TotalSensorsCard, 0);
            Grid.SetColumnSpan(TotalSensorsCard, 1);
            Grid.SetRow(ReportingSensorsCard, 0);
            Grid.SetColumn(ReportingSensorsCard, 1);
            Grid.SetRow(ExceptionsCard, 0);
            Grid.SetColumn(ExceptionsCard, 2);
        }
        else
        {
            ReportSummaryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportSummaryGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ReportSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ReportSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            Grid.SetRow(TotalSensorsCard, 0);
            Grid.SetColumn(TotalSensorsCard, 0);
            Grid.SetColumnSpan(TotalSensorsCard, 2);
            Grid.SetRow(ReportingSensorsCard, 1);
            Grid.SetColumn(ReportingSensorsCard, 0);
            Grid.SetRow(ExceptionsCard, 1);
            Grid.SetColumn(ExceptionsCard, 1);
        }
    }


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

                var nowUtc = DateTime.UtcNow;
                var sensorSnapshots = db.Sensors
                    .AsNoTracking()
                    .Select(sensor => new
                    {
                        LatestTimestamp = sensor.Readings
                            .OrderByDescending(reading => reading.Timestamp)
                            .Select(reading => (DateTime?)reading.Timestamp)
                            .FirstOrDefault()
                    })
                    .ToList();

                var freshSensors = sensorSnapshots.Count(sensor =>
                    SensorFreshnessPolicy.GetState(sensor.LatestTimestamp, nowUtc) == SensorFreshnessState.Fresh);
                var staleSensors = sensorSnapshots.Count(sensor =>
                    SensorFreshnessPolicy.GetState(sensor.LatestTimestamp, nowUtc) == SensorFreshnessState.Stale);
                var offlineSensors = sensorSnapshots.Count(sensor =>
                    SensorFreshnessPolicy.GetState(sensor.LatestTimestamp, nowUtc) == SensorFreshnessState.Missing);
                var latestTimestamp = sensorSnapshots
                    .Where(sensor => sensor.LatestTimestamp.HasValue)
                    .Select(sensor => sensor.LatestTimestamp!.Value)
                    .DefaultIfEmpty()
                    .Max();

                var chartSensors = db.Readings
                    .AsNoTracking()
                    .Where(reading => reading.Sensor.Enabled == 1)
                    .Select(reading => new
                    {
                        SensorId = reading.SensorId,
                        reading.Sensor.Name,
                        reading.Sensor.Type,
                        reading.Metric
                    })
                    .Distinct()
                    .OrderBy(sensor => sensor.Type)
                    .ThenBy(sensor => sensor.Name)
                    .ThenBy(sensor => sensor.Metric)
                    .ToList()
                    .Select(sensor => new ChartSensorOption
                    {
                        SensorId = sensor.SensorId,
                        Name = sensor.Name,
                        DisplayName = $"{sensor.Name} · {sensor.Metric}",
                        Type = sensor.Type,
                        Metric = sensor.Metric
                    })
                    .ToList();

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
                    OnlineSensors = freshSensors,
                    StaleSensors = staleSensors,
                    OfflineSensors = offlineSensors,
                    LatestTimestamp = latestTimestamp == default ? null : latestTimestamp,
                    Alerts = alertCount,
                    ChartSensors = chartSensors
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

                DataStatusLabel.Text = result.LatestTimestamp.HasValue
                    ? $"Fresh {result.OnlineSensors} · Stale {result.StaleSensors} · Offline {result.OfflineSensors} · Latest {result.LatestTimestamp.Value.ToLocalTime():HH:mm}"
                    : "No sensor readings yet";

                AlertCountLabel.Text =
                    result.Alerts.ToString();

                _chartSelectionReady = false;
                ChartSensorPicker.ItemsSource = result.ChartSensors;
                if (result.ChartSensors.Count > 0)
                {
                    var savedChartKey = Preferences.Get("Reports_ChartKey", "");
                    var savedIndex = result.ChartSensors.FindIndex(sensor => sensor.Key == savedChartKey);
                    var phIndex = result.ChartSensors.FindIndex(sensor => sensor.Type.Equals("pH", StringComparison.OrdinalIgnoreCase));
                    ChartSensorPicker.SelectedIndex = savedIndex >= 0 ? savedIndex : phIndex >= 0 ? phIndex : 0;
                }
                _chartSelectionReady = true;

            });


            await LoadChartAsync();

            _lastLoadedUtc = DateTime.UtcNow;

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
        if (_chartSelectionReady)
            _ = LoadChartAsync();
    }

    private void OnChartSensorChanged(object? sender, EventArgs e)
    {
        if (!_chartSelectionReady || ChartSensorPicker.SelectedItem is not ChartSensorOption selected)
            return;

        Preferences.Set("Reports_ChartKey", selected.Key);
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
    // Load selected sensor chart data
    // ============================================================

    private async Task LoadChartAsync()
    {
        LogToFile("=== ReportsPage LoadChart STARTED ===");

        try
        {
            if (ChartSensorPicker.SelectedItem is not ChartSensorOption selectedSensor)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ChartTitleLabel.Text = "Sensor trend";
                    ChartSubtitleLabel.Text = "No sensors have historical readings yet";
                    UpdateChart([], GetSelectedTimeRange(), null);
                });
                return;
            }

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
                        r.SensorId == selectedSensor.SensorId &&
                        r.Metric == selectedSensor.Metric &&
                        r.Timestamp >= cutoff)
                    .OrderBy(r => r.Timestamp)
                    .ToList();
            });


            LogToFile(
                $"=== ReportsPage LoadChart: " +
                $"found {readings.Count} readings for sensor {selectedSensor.SensorId} ===");


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ChartTitleLabel.Text = $"{selectedSensor.Name} trend";
                ChartSubtitleLabel.Text = $"{selectedSensor.Type} · {selectedSensor.Metric}";
                UpdateChart(readings, timeRange, selectedSensor);
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
                    $"Failed to load sensor history:\n\n{ex.Message}",
                    "OK");
            });
        }
    }


    // ============================================================
    // Update Chart
    // ============================================================

    private void UpdateChart(
        List<Reading> readings,
        TimeSpan timeRange,
        ChartSensorOption? selectedSensor)
    {
        var points =
            readings
                .Select(r => new SensorChartPoint
                {
                    Timestamp = r.Timestamp,
                    Value = r.Value
                })
                .OrderBy(p => p.Timestamp)
                .ToList();

        points = Downsample(points, 240);

        _chartDrawable.SetData(
            points,
            timeRange,
            selectedSensor == null ? "No sensor data available" : $"No {selectedSensor.DisplayName} data in this range",
            GetChartColorResource(selectedSensor?.Type));


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


            var unit = selectedSensor?.Metric ?? readings.Last().Metric;
            CurrentValueLabel.Text = FormatChartValue(values.Last(), unit);

            MinValueLabel.Text =
                FormatChartValue(values.Min(), unit);

            MaxValueLabel.Text =
                FormatChartValue(values.Max(), unit);

            ChartStats.IsVisible = true;
        }


        UpdateLegend(readings, selectedSensor);
    }


    // ============================================================
    // Chart Legend
    // ============================================================

    private void UpdateLegend(
        List<Reading> readings,
        ChartSensorOption? selectedSensor)
    {
        ChartLegend.Children.Clear();


        if (readings.Count == 0)
        {
            ChartLegend.Children.Add(
                new Label
                {
                    Text = "No readings in this time range",
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
                    $"{changeIcon} {FormatChartValue(Math.Abs(change), selectedSensor?.Metric ?? latest.Metric)}",

                FontSize = 11,

                TextColor = changeColor,

                FontAttributes =
                    FontAttributes.Bold
            });
    }


    private static List<SensorChartPoint> Downsample(List<SensorChartPoint> points, int maximumPoints)
    {
        if (points.Count <= maximumPoints)
            return points;

        var sampled = new List<SensorChartPoint>(maximumPoints);
        var stride = (points.Count - 1d) / (maximumPoints - 1d);
        for (var index = 0; index < maximumPoints; index++)
            sampled.Add(points[(int)Math.Round(index * stride)]);
        return sampled;
    }

    private static string GetChartColorResource(string? sensorType)
        => sensorType switch
        {
            "SolarPower" or "SolarVoltage" => "Secondary",
            "BatteryPercent" or "BatteryVoltage" => "Tertiary",
            "WaterTemp" or "UltrasonicLevel" or "FlowRate" => "Tertiary",
            _ => "Primary"
        };

    private static string FormatChartValue(double value, string? unit)
    {
        var formatted = Math.Abs(value) >= 100 ? value.ToString("F0") : value.ToString("F1");
        return string.IsNullOrWhiteSpace(unit) ? formatted : $"{formatted} {unit}";
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
                Color.FromArgb("#143A27"));


        var errorColor =
            GetResourceColor(
                "ErrorContainer",
                Color.FromArgb("#3D1D25"));


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
                GetResourceColor("Primary", Color.FromArgb("#74E0A2"));


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
                GetResourceColor("Error", Color.FromArgb("#FF7B86"));


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
        try
        {
            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var rows = await db.Readings
                .AsNoTracking()
                .Where(reading => reading.Timestamp >= cutoff)
                .OrderBy(reading => reading.Timestamp)
                .Select(reading => new
                {
                    SensorName = reading.Sensor.Name,
                    reading.Sensor.Type,
                    reading.Timestamp,
                    reading.Value,
                    reading.Metric
                })
                .ToListAsync();

            if (rows.Count == 0)
            {
                await DisplayAlertAsync("No data", "There are no readings from the last seven days to export.", "OK");
                return;
            }

            var csv = new StringBuilder("Sensor,Type,Timestamp UTC,Value,Metric\r\n");
            foreach (var row in rows)
            {
                csv.Append(Csv(row.SensorName)).Append(',')
                    .Append(Csv(row.Type)).Append(',')
                    .Append(row.Timestamp.ToUniversalTime().ToString("O")).Append(',')
                    .Append(row.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(Csv(row.Metric ?? "")).Append("\r\n");
            }

            var exportPath = Path.Combine(FileSystem.CacheDirectory, $"glorious_readings_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            await File.WriteAllTextAsync(exportPath, csv.ToString());
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export GLORIOUS SYSTEM readings",
                File = new ShareFile(exportPath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export failed", ex.Message, "OK");
        }
    }


    private async void OnViewLogsClicked(
        object sender,
        EventArgs e)
    {
        try
        {
            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensorCount = await db.Sensors.CountAsync();
            var readingCount = await db.Readings.CountAsync();
            var scanCount = await db.LeafClassifications.CountAsync();
            var latestReading = await db.Readings.MaxAsync(reading => (DateTime?)reading.Timestamp);

            var lastUpdate = latestReading.HasValue
                ? latestReading.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                : "No readings yet";
            await DisplayAlertAsync(
                "System details",
                $"Platform: {DeviceInfo.Current.Platform}\nSensors: {sensorCount}\nStored readings: {readingCount}\nSaved crop scans: {scanCount}\nLatest reading: {lastUpdate}",
                "Close");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("System details unavailable", ex.Message, "OK");
        }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";


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

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _lastLoadedUtc = DateTime.MinValue;
        Dispatcher.Dispatch(ReadingsChart.Invalidate);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (DateTime.UtcNow - _lastLoadedUtc >= AutomaticRefreshInterval)
            Dispatcher.Dispatch(() => _ = LoadAsync());
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

    public int StaleSensors { get; set; }

    public int OfflineSensors { get; set; }

    public DateTime? LatestTimestamp { get; set; }

    public int Alerts { get; set; }

    public List<ChartSensorOption> ChartSensors { get; set; } = [];
}


// ====================================================================
// Sensor chart models
// ====================================================================

internal sealed class SensorChartPoint
{
    public DateTime Timestamp { get; set; }

    public double Value { get; set; }
}

internal sealed class ChartSensorOption
{
    public int SensorId { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Metric { get; set; } = "";
    public string Key => $"{SensorId}:{Metric}";
}
