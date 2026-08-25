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
    double _thresholdProgress = 0;
    bool _hasUnit = false;
    bool _hasThresholds = false;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string SubText
    {
        get => _subText;
        set { _subText = value; OnPropertyChanged(); }
    }

    public string ValueText
    {
        get => _valueText;
        set { _valueText = value; OnPropertyChanged(); }
    }

    public string UnitText
    {
        get => _unitText;
        set { _unitText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnit)); }
    }

    public string MinThresholdText
    {
        get => _minThresholdText;
        set { _minThresholdText = value; OnPropertyChanged(); }
    }

    public string MaxThresholdText
    {
        get => _maxThresholdText;
        set { _maxThresholdText = value; OnPropertyChanged(); }
    }

    public Color StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public Color ValueColor
    {
        get => _valueColor;
        set { _valueColor = value; OnPropertyChanged(); }
    }

    public Color ThresholdProgressColor
    {
        get => _thresholdProgressColor;
        set { _thresholdProgressColor = value; OnPropertyChanged(); }
    }

    public double ThresholdProgress
    {
        get => _thresholdProgress;
        set { _thresholdProgress = value; OnPropertyChanged(); }
    }

    public bool HasUnit => !string.IsNullOrEmpty(_unitText);

    public bool HasThresholds
    {
        get => _hasThresholds;
        set { _hasThresholds = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SensorGroup : ObservableCollection<SensorDisplayItem>
{
    public string CategoryName { get; set; } = "";
    public string CategoryIcon { get; set; } = "";
    public int OnlineCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public int OfflineCount { get; set; }

    public Color OnlineColor => Color.FromArgb("#10B981");
    public Color WarningColor => Color.FromArgb("#F59E0B");
    public Color CriticalColor => Color.FromArgb("#EF4444");

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
    static readonly Color HasDataColor = Color.FromArgb("#10B981");
    static readonly Color NoDataColor = Color.FromArgb("#64748B");
    static readonly Color WarningColor = Color.FromArgb("#F59E0B");
    static readonly Color CriticalColor = Color.FromArgb("#EF4444");

    bool _isRefreshing = false;
    bool _isFirstLoad = true;

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

    public MainPage()
    {
        LogToFile("=== MainPage constructor STARTED ===");
        System.Diagnostics.Debug.WriteLine("=== MainPage constructor STARTED ===");
        try
        {
            InitializeComponent();
            LogToFile("=== MainPage InitializeComponent COMPLETED ===");
            System.Diagnostics.Debug.WriteLine("=== MainPage InitializeComponent COMPLETED ===");
            LoadSensors();
            LogToFile("=== MainPage LoadSensors CALLED ===");
            System.Diagnostics.Debug.WriteLine("=== MainPage LoadSensors CALLED ===");
        }
        catch (Exception ex)
        {
            LogToFile($"!!! MainPage constructor FAILED: {ex}");
            System.Diagnostics.Debug.WriteLine($"!!! MainPage constructor FAILED: {ex}");
            throw;
        }
        LogToFile("=== MainPage constructor COMPLETED ===");
        System.Diagnostics.Debug.WriteLine("=== MainPage constructor COMPLETED ===");
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
            await Task.Run(LoadSensors);
        }
        finally
        {
            await Task.Delay(500); // Show refresh indicator briefly
            IsRefreshing = false;
        }
    }

    void OnRefreshClicked(object sender, EventArgs e) => _ = RefreshAsync();

    void LoadSensors()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = db.Sensors.Include(s => s.Readings).ToList();

            SensorDisplayItem ToItem(Sensor s)
            {
                var latest = s.Readings.OrderByDescending(r => r.Timestamp).FirstOrDefault();
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
                    bool nearThreshold = false;

                    if (s.MinThreshold.HasValue || s.MaxThreshold.HasValue)
                    {
                        item.HasThresholds = true;
                        item.MinThresholdText = s.MinThreshold.HasValue ? $"{s.MinThreshold.Value:F1}" : "--";
                        item.MaxThresholdText = s.MaxThreshold.HasValue ? $"{s.MaxThreshold.Value:F1}" : "--";

                        double min = s.MinThreshold ?? double.MinValue;
                        double max = s.MaxThreshold ?? double.MaxValue;
                        double range = max - min;

                        if (range > 0 && latest.Value >= min && latest.Value <= max)
                        {
                            item.ThresholdProgress = (latest.Value - min) / range;
                        }
                        else if (latest.Value < min)
                        {
                            item.ThresholdProgress = 0;
                        }
                        else
                        {
                            item.ThresholdProgress = 1;
                        }

                        if (outOfRange)
                        {
                            item.StatusColor = CriticalColor;
                            item.ValueColor = CriticalColor;
                            item.ThresholdProgressColor = CriticalColor;
                        }
                        else
                        {
                            // Check if near threshold (within 10%)
                            double lowerBound = min + range * 0.1;
                            double upperBound = max - range * 0.1;
                            if ((s.MinThreshold.HasValue && latest.Value <= lowerBound) ||
                                (s.MaxThreshold.HasValue && latest.Value >= upperBound))
                            {
                                nearThreshold = true;
                                item.StatusColor = WarningColor;
                                item.ValueColor = WarningColor;
                                item.ThresholdProgressColor = WarningColor;
                            }
                            else
                            {
                                item.StatusColor = HasDataColor;
                                item.ValueColor = HasDataColor;
                                item.ThresholdProgressColor = HasDataColor;
                            }
                        }
                    }
                    else
                    {
                        item.StatusColor = HasDataColor;
                        item.ValueColor = HasDataColor;
                    }
                }
                else
                {
                    item.ValueText = "--";
                    item.StatusColor = NoDataColor;
                    item.ValueColor = NoDataColor;
                }

                return item;
            }

            var waterQualitySensors = sensors.Where(s => new[] { "pH", "TDS", "WaterTemp", "UltrasonicLevel" }.Contains(s.Type)).ToList();
            var environmentalSensors = sensors.Where(s => s.Type == "BME280").ToList();
            var flowSensors = sensors.Where(s => s.Type == "FlowRate").ToList();

            var groups = new ObservableCollection<SensorGroup>
            {
                CreateGroup("WATER QUALITY", "💧", waterQualitySensors, ToItem),
                CreateGroup("ENVIRONMENTAL", "🌡️", environmentalSensors, ToItem),
                CreateGroup("WATER FLOW", "💨", flowSensors, ToItem),
            };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SensorList.ItemsSource = groups;

                // Update last updated timestamp
                LastUpdatedLabel.Text = DateTime.Now.ToString("HH:mm:ss");

                // Trigger entrance animations on first load
                if (_isFirstLoad)
                {
                    _isFirstLoad = false;
                    AnimateEntrance(groups);
                }
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Error", $"Failed to load sensors: {ex.Message}", "OK");
            });
        }
    }

    SensorGroup CreateGroup(string name, string icon, List<Sensor> sensors, Func<Sensor, SensorDisplayItem> selector)
    {
        var items = sensors.Select(selector).ToList();
        var group = new SensorGroup(name, icon, items)
        {
            OnlineCount = items.Count(i => i.StatusColor == HasDataColor),
            WarningCount = items.Count(i => i.StatusColor == WarningColor),
            CriticalCount = items.Count(i => i.StatusColor == CriticalColor),
            OfflineCount = items.Count(i => i.StatusColor == NoDataColor)
        };
        return group;
    }

    async void AnimateEntrance(ObservableCollection<SensorGroup> groups)
    {
        // Fade in the collection view
        SensorList.Opacity = 0;
        await SensorList.FadeToAsync(1, 300, Easing.CubicOut);

        // Stagger animate each group header and items
        // Note: MAUI CollectionView doesn't easily expose item views for stagger animation
        // This is a simplified entrance - in production you'd use a custom layout or behaviors
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_isFirstLoad)
        {
            LoadSensors();
        }
    }
}