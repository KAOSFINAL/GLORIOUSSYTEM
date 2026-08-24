using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GLORIOUSSYSTEM.Data.Models;
using Microcharts;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace GLORIOUSSYSTEM.App;

public partial class ReportsPage : ContentPage
{
    bool _isRefreshing = false;
    bool _isFirstLoad = true;

    public ReportsPage()
    {
        InitializeComponent();
        ChartTimeRangePicker.SelectedIndex = 2; // 24 Hours default
        ChartTimeRangePicker.SelectedIndexChanged += OnTimeRangeChanged;
        Load();
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
            RefreshView.IsRefreshing = value;
        }
    }

    public ICommand RefreshCommand => new Command(async () => await RefreshAsync());

    async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            await Task.Run(Load);
        }
        finally
        {
            await Task.Delay(500);
            IsRefreshing = false;
        }
    }

    void OnTimeRangeChanged(object? sender, EventArgs e)
    {
        LoadChart();
    }

    void Load()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            // Summary stats
            var sensors = db.Sensors.ToList();
            var readingsWithData = db.Readings.Select(r => r.SensorId).Distinct().Count();
            var offlineCount = sensors.Count - readingsWithData;

            // Count alerts (readings out of threshold)
            var alertCount = db.Sensors
                .Where(s => s.MinThreshold.HasValue || s.MaxThreshold.HasValue)
                .Select(s => new { Sensor = s, Latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault() })
                .Where(x => x.Latest != null &&
                    ((x.Sensor.MinThreshold.HasValue && x.Latest.Value < x.Sensor.MinThreshold.Value) ||
                     (x.Sensor.MaxThreshold.HasValue && x.Latest.Value > x.Sensor.MaxThreshold.Value)))
                .Count();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TotalSensorsLabel.Text = sensors.Count.ToString();
                OnlineSensorsLabel.Text = readingsWithData.ToString();
                AlertCountLabel.Text = alertCount.ToString();

                // Animate counter entrance
                if (_isFirstLoad)
                {
                    AnimateCounters();
                    _isFirstLoad = false;
                }
            });

            LoadChart();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlertAsync("Error", $"Failed to load reports: {ex.Message}", "OK");
            });
        }
    }

    void LoadChart()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            var timeRange = ChartTimeRangePicker.SelectedIndex switch
            {
                0 => TimeSpan.FromHours(1),
                1 => TimeSpan.FromHours(6),
                2 => TimeSpan.FromHours(24),
                3 => TimeSpan.FromDays(7),
                _ => TimeSpan.FromHours(24)
            };

            var cutoff = DateTime.UtcNow - timeRange;

            var readings = db.Readings
                .Where(r => r.Metric == "pH" && r.Timestamp >= cutoff)
                .OrderBy(r => r.Timestamp)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Create chart entries
                var entries = readings.Select((r, i) => new ChartEntry((float)r.Value)
                {
                    Label = r.Timestamp.ToString(timeRange <= TimeSpan.FromHours(6) ? "HH:mm" : "MM/dd"),
                    ValueLabel = r.Value.ToString("F1"),
                    Color = SKColor.Parse("#10B981"),
                    TextColor = SKColor.Parse("#1C1C1E"),
                    ValueLabelColor = SKColor.Parse("#10B981")
                }).ToArray();

                // Create gradient line chart
                var chart = new LineChart
                {
                    Entries = entries,
                    LineMode = LineMode.Spline,
                    LineSize = 3,
                    PointMode = PointMode.Circle,
                    PointSize = 6,
                    BackgroundColor = SKColor.Parse("#FAFAFA"),
                    Margin = 20,
                    LabelTextSize = 28,
                    ValueLabelTextSize = 32,
                    ValueLabelOrientation = Orientation.Horizontal,
                    IsAnimated = true,
                    AnimationDuration = TimeSpan.FromMilliseconds(800)
                };

                // Dark theme support
                if (App.Current?.RequestedTheme == AppTheme.Dark)
                {
                    chart.BackgroundColor = SKColor.Parse("#1C1C1E");
                    chart.LabelTextSize = 28;
                }

                ReadingsChart.Chart = chart;

                // Update chart stats
                if (readings.Count > 0)
                {
                    var values = readings.Select(r => r.Value).ToList();
                    CurrentValueLabel.Text = values.Last().ToString("F1");
                    MinValueLabel.Text = values.Min().ToString("F1");
                    MaxValueLabel.Text = values.Max().ToString("F1");
                    ChartStats.IsVisible = true;
                }
                else
                {
                    ChartStats.IsVisible = false;
                }

                // Update legend
                UpdateLegend(readings);
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlertAsync("Error", $"Failed to load chart: {ex.Message}", "OK");
            });
        }
    }

    void UpdateLegend(List<Reading> readings)
    {
        ChartLegend.Children.Clear();

        if (readings.Count == 0)
        {
            ChartLegend.Children.Add(new Label
            {
                Text = "No pH data available",
                FontSize = 12,
                TextColor = (Color)Application.Current?.Resources["OnSurfaceVariant"] ?? Colors.Gray
            });
            return;
        }

        var latest = readings.Last();
        var earliest = readings.First();
        var change = latest.Value - earliest.Value;
        var changeColor = change >= 0 ? (Color)Resources["StatusOnline"] : (Color)Resources["StatusCritical"];
        var changeIcon = change >= 0 ? "↗" : "↘";

        ChartLegend.Children.Add(new Label
        {
            Text = $"Range: {earliest.Value:F1} – {latest.Value:F1}",
            FontSize = 12,
            TextColor = (Color)Application.Current?.Resources["OnSurfaceVariant"] ?? Colors.Gray
        });

        ChartLegend.Children.Add(new Label
        {
            Text = $"{changeIcon} {Math.Abs(change):F1} pH",
            FontSize = 11,
            TextColor = changeColor,
            FontAttributes = FontAttributes.Bold
        });
    }

    void AnimateCounters()
    {
        // Simple counter animation for summary cards
        AnimateCounter(TotalSensorsLabel, 0, int.Parse(TotalSensorsLabel.Text), 600);
        AnimateCounter(OnlineSensorsLabel, 0, int.Parse(OnlineSensorsLabel.Text), 600, 100);
        AnimateCounter(AlertCountLabel, 0, int.Parse(AlertCountLabel.Text), 600, 200);
    }

    async void AnimateCounter(Label label, int from, int to, int duration, int delay = 0)
    {
        if (delay > 0) await Task.Delay(delay);
        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalMilliseconds < duration)
        {
            var progress = (DateTime.Now - startTime).TotalMilliseconds / duration;
            var eased = Easing.CubicOut.Ease(progress);
            var current = (int)(from + (to - from) * eased);
            label.Text = current.ToString();
            await Task.Delay(16);
        }
        label.Text = to.ToString();
    }

    async void OnTestApiClicked(object sender, EventArgs e)
    {
        TestApiButton.IsEnabled = false;
        ApiTestLoading.IsVisible = true;
        ApiTestLoading.IsRunning = true;
        ApiResultCard.IsVisible = false;
        ApiErrorLabel.IsVisible = false;
        ApiStatusLabel.Text = "Testing...";

        var successColor = (Color)Application.Current?.Resources["StatusOnline"] ?? Color.FromArgb("#10B981");
        var errorColor = (Color)Application.Current?.Resources["Error"] ?? Color.FromArgb("#DC2626");

        try
        {
            var api = new ApiSensorService();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var readings = await api.GetLatestAsync();
            stopwatch.Stop();

            var withData = readings.Count(r => r.LatestValue.HasValue);

            ApiResultCard.BackgroundColor = successColor;
            ApiStatusIconLabel.Text = "✓";
            ApiStatusIconLabel.TextColor = Colors.White;
            ApiStatusLabel.Text = "Connected";
            ApiDetailLabel.Text = $"{readings.Count} sensors, {withData} with live data";
            ApiLatencyLabel.Text = $"{stopwatch.ElapsedMilliseconds} ms";
            ApiErrorLabel.IsVisible = false;

            ApiResultCard.IsVisible = true;

            // Animate result card
            ApiResultCard.Opacity = 0;
            ApiResultCard.TranslationY = 10;
            await Task.WhenAll(
                ApiResultCard.FadeToAsync(1, 300, Easing.CubicOut),
                ApiResultCard.TranslateToAsync(0, 0, 300, Easing.CubicOut)
            );
        }
        catch (Exception ex)
        {
            ApiResultCard.BackgroundColor = errorColor;
            ApiStatusIconLabel.Text = "✕";
            ApiStatusIconLabel.TextColor = Colors.White;
            ApiStatusLabel.Text = "Connection Failed";
            ApiDetailLabel.Text = ex.Message;
            ApiLatencyLabel.Text = "-- ms";
            ApiErrorLabel.Text = $"Error: {ex.Message}";
            ApiErrorLabel.IsVisible = true;
            ApiResultCard.IsVisible = true;
        }
        finally
        {
            TestApiButton.IsEnabled = true;
            ApiTestLoading.IsVisible = false;
            ApiTestLoading.IsRunning = false;
        }
    }

    void OnExportDataClicked(object sender, EventArgs e)
    {
        DisplayAlert("Export Data", "Data export functionality coming soon!", "OK");
    }

    void OnViewLogsClicked(object sender, EventArgs e)
    {
        DisplayAlert("View Logs", "System logs viewer coming soon!", "OK");
    }

    void OnCalibrateClicked(object sender, EventArgs e)
    {
        DisplayAlert("Calibrate Sensors", "Sensor calibration wizard coming soon!", "OK");
    }

    void OnSystemInfoClicked(object sender, EventArgs e)
    {
        DisplayAlert("System Info", "GLORIOUS SYSTEM v1.0\n.NET 10 MAUI App\nSQLite + EF Core\nONNX Runtime CNN", "OK");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_isFirstLoad)
        {
            Load();
        }
    }
}