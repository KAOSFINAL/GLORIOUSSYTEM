using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public class SensorDisplayItem : INotifyPropertyChanged
{
    string _name = "";
    string _subText = "";
    string _valueText = "--";
    string _unitText = "";
    string _minThresholdText = "";
    string _maxThresholdText = "";
    Color _statusColor = Colors.Gray;
    Color _valueColor = Colors.Gray;
    Color _thresholdProgressColor = Colors.Gray;
    string _statusText = "No recent data";
    double _thresholdProgress = 0;
    bool _hasThresholds = false;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string SubText { get => _subText; set { _subText = value; OnPropertyChanged(); } }
    public string ValueText { get => _valueText; set { _valueText = value; OnPropertyChanged(); } }
    public string UnitText { get => _unitText; set { _unitText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnit)); } }
    public string MinThresholdText { get => _minThresholdText; set { _minThresholdText = value; OnPropertyChanged(); } }
    public string MaxThresholdText { get => _maxThresholdText; set { _maxThresholdText = value; OnPropertyChanged(); } }
    public Color StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(); } }
    public Color ValueColor { get => _valueColor; set { _valueColor = value; OnPropertyChanged(); } }
    public Color ThresholdProgressColor { get => _thresholdProgressColor; set { _thresholdProgressColor = value; OnPropertyChanged(); } }
    public double ThresholdProgress { get => _thresholdProgress; set { _thresholdProgress = value; OnPropertyChanged(); } }
    public bool HasUnit => !string.IsNullOrEmpty(_unitText);
    public bool HasThresholds { get => _hasThresholds; set { _hasThresholds = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SensorGroup : ObservableCollection<SensorDisplayItem>
{
    public string CategoryName { get; set; } = "";
    public string CategoryIcon { get; set; } = "";
    public int OnlineCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public int OfflineCount { get; set; }

    public Color OnlineColor => Color.FromArgb("#5EE0A0");
    public Color WarningColor => Color.FromArgb("#F4C95D");
    public Color CriticalColor => Color.FromArgb("#FF7185");

    public bool HasOnline => OnlineCount > 0;
    public bool HasWarning => WarningCount > 0;
    public bool HasCritical => CriticalCount > 0;
    public int SensorCount => Count;

    public SensorGroup(string name, string icon, IEnumerable<SensorDisplayItem> items) : base(items)
    {
        CategoryName = name;
        CategoryIcon = icon;
    }
}

public partial class MainPage : ContentPage
{
    sealed class ReadingSnapshot
    {
        public double Value { get; init; }
        public string? Metric { get; init; }
        public DateTime Timestamp { get; init; }
    }

    sealed class SensorSnapshot
    {
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public string? Model { get; init; }
        public double? MinThreshold { get; init; }
        public double? MaxThreshold { get; init; }
        public ReadingSnapshot? Latest { get; init; }
    }

    static readonly Color HasDataColor = Color.FromArgb("#5EE0A0");
    static readonly Color NoDataColor = Color.FromArgb("#A8B5C2");
    static readonly Color WarningColor = Color.FromArgb("#F4C95D");
    static readonly Color CriticalColor = Color.FromArgb("#FF7185");

    bool _isRefreshing;
    bool _isLoading;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
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
        try { await LoadSensorsAsync(); }
        finally { IsRefreshing = false; }
    }

    void OnRefreshClicked(object sender, EventArgs e) => _ = RefreshAsync();

    async void OnReportsRequested(object sender, EventArgs e) => await Shell.Current.GoToAsync("//reports");

    async void OnScanRequested(object sender, EventArgs e) => await Shell.Current.GoToAsync("//webcam");

    async Task LoadSensorsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            using var scope = (App.Services ?? throw new InvalidOperationException("Application services are unavailable.")).CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = await db.Sensors
                .AsNoTracking()
                .Select(s => new SensorSnapshot
                {
                    Name = s.Name,
                    Type = s.Type,
                    Model = s.Model,
                    MinThreshold = s.MinThreshold,
                    MaxThreshold = s.MaxThreshold,
                    Latest = s.Readings
                        .OrderByDescending(r => r.Timestamp)
                        .Select(r => new ReadingSnapshot
                        {
                            Value = r.Value,
                            Metric = r.Metric,
                            Timestamp = r.Timestamp
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            SensorDisplayItem ToItem(SensorSnapshot s)
            {
                var latest = s.Latest;
                var item = new SensorDisplayItem
                {
                    Name = s.Name,
                    SubText = s.Model ?? s.Type,
                    UnitText = latest?.Metric ?? ""
                };

                if (latest != null)
                {
                    item.ValueText = latest.Value.ToString("F1");
                    item.UnitText = latest.Metric ?? "";

                    bool outOfRange = (s.MinThreshold.HasValue && latest.Value < s.MinThreshold.Value) ||
                                      (s.MaxThreshold.HasValue && latest.Value > s.MaxThreshold.Value);

                    if (s.MinThreshold.HasValue || s.MaxThreshold.HasValue)
                    {
                        item.HasThresholds = true;
                        item.MinThresholdText = s.MinThreshold.HasValue ? $"{s.MinThreshold.Value:F1}" : "--";
                        item.MaxThresholdText = s.MaxThreshold.HasValue ? $"{s.MaxThreshold.Value:F1}" : "--";

                        var min = s.MinThreshold;
                        var max = s.MaxThreshold;

                        if (min.HasValue && max.HasValue && max.Value > min.Value)
                            item.ThresholdProgress = Math.Clamp((latest.Value - min.Value) / (max.Value - min.Value), 0, 1);
                        else
                            item.ThresholdProgress = 0.5;

                        if (outOfRange)
                        {
                            item.StatusColor = CriticalColor;
                            item.ValueColor = CriticalColor;
                            item.ThresholdProgressColor = CriticalColor;
                            item.StatusText = "Needs attention";
                        }
                        else
                        {
                            var nearMinimum = min.HasValue && latest.Value <= min.Value + Math.Max(Math.Abs(min.Value) * 0.1, 0.1);
                            var nearMaximum = max.HasValue && latest.Value >= max.Value - Math.Max(Math.Abs(max.Value) * 0.1, 0.1);
                            if (nearMinimum || nearMaximum)
                            {
                                item.StatusColor = WarningColor;
                                item.ValueColor = WarningColor;
                                item.ThresholdProgressColor = WarningColor;
                                item.StatusText = "Near limit";
                            }
                            else
                            {
                                item.StatusColor = HasDataColor;
                                item.ValueColor = HasDataColor;
                                item.ThresholdProgressColor = HasDataColor;
                                item.StatusText = "In range";
                            }
                        }
                    }
                    else
                    {
                        item.StatusColor = HasDataColor;
                        item.ValueColor = HasDataColor;
                        item.StatusText = "Live";
                    }
                }
                else
                {
                    item.ValueText = "--";
                    item.StatusColor = NoDataColor;
                    item.ValueColor = NoDataColor;
                    item.StatusText = "No recent data";
                }

                return item;
            }

            var waterQualitySensors = sensors.Where(s => new[] { "pH", "EC", "TDS", "WaterTemp", "UltrasonicLevel" }.Contains(s.Type)).ToList();
            var environmentalSensors = sensors.Where(s => s.Type == "BME280").ToList();
            var flowSensors = sensors.Where(s => s.Type == "FlowRate").ToList();
            var solarSensors = sensors.Where(s => new[] { "SolarPower", "SolarVoltage", "BatteryPercent", "BatteryVoltage" }.Contains(s.Type)).ToList();

            var ecSensor = sensors.FirstOrDefault(s => s.Type == "EC")
                ?? sensors.FirstOrDefault(s => s.Type == "TDS");
            var solarSensor = sensors.FirstOrDefault(s => s.Type == "SolarPower")
                ?? sensors.FirstOrDefault(s => s.Type == "SolarVoltage");
            var batterySensor = sensors.FirstOrDefault(s => s.Type == "BatteryPercent")
                ?? sensors.FirstOrDefault(s => s.Type == "BatteryVoltage");

            var ecReading = ecSensor?.Latest;
            var solarReading = solarSensor?.Latest;
            var batteryReading = batterySensor?.Latest;

            var groups = new ObservableCollection<SensorGroup>
            {
                CreateGroup("WATER QUALITY", "sensor_water.svg", waterQualitySensors, ToItem),
                CreateGroup("ENVIRONMENT", "sensor_environment.svg", environmentalSensors, ToItem),
                CreateGroup("WATER FLOW", "sensor_flow.svg", flowSensors, ToItem),
            };

            if (solarSensors.Count > 0)
                groups.Add(CreateGroup("SOLAR POWER", "sensor_solar.svg", solarSensors, ToItem));

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BindableLayout.SetItemsSource(SensorList, groups);
                EcValueLabel.Text = ecReading?.Value.ToString("F1") ?? "--";
                EcUnitLabel.Text = ecReading?.Metric ?? "";
                EcSourceLabel.Text = ecSensor == null ? "Add EC sensor" : ecSensor.Type == "TDS" ? "TDS source" : "Conductivity source";

                SolarPowerValueLabel.Text = solarReading?.Value.ToString("F0") ?? "--";
                SolarPowerUnitLabel.Text = solarReading?.Metric ?? "";
                SolarSourceLabel.Text = solarSensor == null ? "Add SolarPower sensor" : solarSensor.Name;

                BatteryValueLabel.Text = batteryReading?.Value.ToString("F0") ?? "--";
                BatteryUnitLabel.Text = batteryReading?.Metric ?? "";
                BatterySourceLabel.Text = batterySensor == null ? "Add battery sensor" : batterySensor.Name;
                var latestTimestamp = sensors.Where(s => s.Latest != null).Select(s => s.Latest!.Timestamp).DefaultIfEmpty().Max();
                LastUpdatedLabel.Text = latestTimestamp == default
                    ? "No readings yet"
                    : $"Updated {latestTimestamp.ToLocalTime():HH:mm}";
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await DisplayAlertAsync("Sensor data unavailable", ex.Message, "OK"));
        }
        finally
        {
            _isLoading = false;
        }
    }

    SensorGroup CreateGroup(string name, string icon, List<SensorSnapshot> sensors, Func<SensorSnapshot, SensorDisplayItem> selector)
    {
        var items = sensors.Select(selector).ToList();
        return new SensorGroup(name, icon, items)
        {
            OnlineCount = items.Count(i => i.StatusColor == HasDataColor),
            WarningCount = items.Count(i => i.StatusColor == WarningColor),
            CriticalCount = items.Count(i => i.StatusColor == CriticalColor),
            OfflineCount = items.Count(i => i.StatusColor == NoDataColor)
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Yield();
        await LoadSensorsAsync();
    }
}
