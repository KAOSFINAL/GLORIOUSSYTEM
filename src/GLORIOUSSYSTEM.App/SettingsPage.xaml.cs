using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public class NotZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index && index != 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SensorSetting : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _model = "";
    private string _type = "";
    private bool _enabled;
    private double? _minThreshold;
    private double? _maxThreshold;
    private bool _hasChanges;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; MarkChanged(); OnPropertyChanged(); } }
    public string Model { get => _model; set { _model = value; OnPropertyChanged(); } }
    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Category));
            OnPropertyChanged(nameof(CategoryIcon));
            OnPropertyChanged(nameof(CategoryMaterialIcon));
            OnPropertyChanged(nameof(CategoryColor));
            OnPropertyChanged(nameof(CategoryContainerColor));
            NotifyRangeConfiguration();
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThresholdStatusText));
            OnPropertyChanged(nameof(ThresholdStatusColor));
            OnPropertyChanged(nameof(ThresholdStatusTextColor));
        }
    }

    public double? MinThreshold
    {
        get => _minThreshold;
        set
        {
            _minThreshold = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MinimumControlValue));
            OnPropertyChanged(nameof(MinimumDisplay));
            NotifyThresholdState();
        }
    }

    public double? MaxThreshold
    {
        get => _maxThreshold;
        set
        {
            _maxThreshold = value;
            MarkChanged();
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaximumControlValue));
            OnPropertyChanged(nameof(MaximumDisplay));
            NotifyThresholdState();
        }
    }

    public bool HasThresholds => MinThreshold.HasValue || MaxThreshold.HasValue;
    public bool HasChanges { get => _hasChanges; private set { _hasChanges = value; OnPropertyChanged(); } }

    public string Category
    {
        get
        {
            var value = $"{Type} {Name}".ToLowerInvariant();
            if (value.Contains("ph") || value.Contains("ec") || value.Contains("water") || value.Contains("flow") || value.Contains("tds") || value.Contains("level"))
                return "WATER";
            return "ENVIRONMENT";
        }
    }

    public string CategoryIcon => Category == "WATER" ? "W" : "E";
    public MaterialIconKind CategoryMaterialIcon => Category == "WATER" ? MaterialIconKind.Water : MaterialIconKind.Environment;
    public Color CategoryColor => Category == "WATER" ? Color.FromArgb("#67C4F4") : Color.FromArgb("#74E0A2");
    public Color CategoryContainerColor => Category == "WATER" ? Color.FromArgb("#123246") : Color.FromArgb("#143A27");

    public string ThresholdStatusText => !Enabled ? "DISABLED" : !HasThresholds ? "SET LIMITS" : "ACTIVE";
    public Color ThresholdStatusColor => !Enabled ? Color.FromArgb("#294738") : !HasThresholds ? Color.FromArgb("#3A3014") : Color.FromArgb("#143A27");
    public Color ThresholdStatusTextColor => !Enabled ? Color.FromArgb("#F1F7F3") : !HasThresholds ? Color.FromArgb("#FFF3C4") : Color.FromArgb("#D9F7E6");

    public double RangeMinimum => GetRange().Minimum;
    public double RangeMaximum => GetRange().Maximum;
    public double RangeStep => GetRange().Step;
    public double RecommendedMinimum => GetRange().RecommendedMinimum;
    public double RecommendedMaximum => GetRange().RecommendedMaximum;
    public string UnitLabel => GetRange().Unit;
    public string RangeDescription => $"Available {Format(RangeMinimum)}–{Format(RangeMaximum)} {UnitLabel}".TrimEnd();
    public string RecommendedDescription => $"Recommended {Format(RecommendedMinimum)}–{Format(RecommendedMaximum)} {UnitLabel}".TrimEnd();

    public double MinimumControlValue
    {
        get => MinThreshold ?? RecommendedMinimum;
        set
        {
            var ceiling = MaxThreshold ?? RecommendedMaximum;
            MinThreshold = Math.Min(RoundToStep(value), ceiling);
        }
    }

    public double MaximumControlValue
    {
        get => MaxThreshold ?? RecommendedMaximum;
        set
        {
            var floor = MinThreshold ?? RecommendedMinimum;
            MaxThreshold = Math.Max(RoundToStep(value), floor);
        }
    }

    public string MinimumDisplay => HasThresholds ? $"{Format(MinimumControlValue)} {UnitLabel}".TrimEnd() : "Not set";
    public string MaximumDisplay => HasThresholds ? $"{Format(MaximumControlValue)} {UnitLabel}".TrimEnd() : "Not set";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ResetChanges() => HasChanges = false;

    public void ApplyRecommended()
    {
        MinThreshold = RecommendedMinimum;
        MaxThreshold = RecommendedMaximum;
    }

    public void ClearThresholds()
    {
        MinThreshold = null;
        MaxThreshold = null;
    }

    private void MarkChanged() => HasChanges = true;
    private void NotifyThresholdState()
    {
        OnPropertyChanged(nameof(HasThresholds));
        OnPropertyChanged(nameof(ThresholdStatusText));
        OnPropertyChanged(nameof(ThresholdStatusColor));
        OnPropertyChanged(nameof(ThresholdStatusTextColor));
    }

    private void NotifyRangeConfiguration()
    {
        OnPropertyChanged(nameof(RangeMinimum));
        OnPropertyChanged(nameof(RangeMaximum));
        OnPropertyChanged(nameof(RangeStep));
        OnPropertyChanged(nameof(RecommendedMinimum));
        OnPropertyChanged(nameof(RecommendedMaximum));
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(RangeDescription));
        OnPropertyChanged(nameof(RecommendedDescription));
        OnPropertyChanged(nameof(MinimumControlValue));
        OnPropertyChanged(nameof(MaximumControlValue));
        OnPropertyChanged(nameof(MinimumDisplay));
        OnPropertyChanged(nameof(MaximumDisplay));
    }

    private double RoundToStep(double value)
    {
        var step = Math.Max(RangeStep, 0.01);
        var rounded = Math.Round(value / step) * step;
        return Math.Clamp(rounded, RangeMinimum, RangeMaximum);
    }

    private string Format(double value)
        => RangeStep < 1 ? value.ToString("F1", CultureInfo.InvariantCulture) : value.ToString("F0", CultureInfo.InvariantCulture);

    private SensorRange GetRange() => Type switch
    {
        "pH" => new(0, 14, 0.1, 5.5, 6.5, "pH"),
        "EC" => new(0, 5, 0.1, 1.2, 2.4, "mS/cm"),
        "TDS" => new(0, 2000, 10, 600, 1000, "ppm"),
        "WaterTemp" => new(0, 40, 0.5, 18, 26, "°C"),
        "UltrasonicLevel" => new(0, 200, 1, 20, 120, "cm"),
        "BME680" => new(-10, 60, 0.5, 18, 30, "°C"),
        "FlowRate" => new(0, 20, 0.1, 1, 5, "L/min"),
        "SolarPower" => new(0, 1000, 5, 50, 800, "W"),
        "SolarVoltage" => new(0, 100, 0.5, 12, 60, "V"),
        "BatteryPercent" => new(0, 100, 1, 20, 95, "%"),
        "BatteryVoltage" => new(0, 60, 0.1, 11.5, 54, "V"),
        _ => new(0, 100, 1, 20, 80, "")
    };

    private readonly record struct SensorRange(
        double Minimum,
        double Maximum,
        double Step,
        double RecommendedMinimum,
        double RecommendedMaximum,
        string Unit);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SettingsPage : ContentPage, INotifyPropertyChanged
{
    private readonly ObservableCollection<SensorSetting> _settings = new();
    private bool _hasUnsavedChanges;
    private bool _darkModeEnabled;
    private bool _isLoading;
    private bool _hasLoaded;
    private bool _isInitializingThemeControls;

    public bool DarkModeEnabled
    {
        get => _darkModeEnabled;
        set { _darkModeEnabled = value; OnPropertyChanged(); }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        BindableLayout.SetItemsSource(SettingsList, _settings);
        PrimaryColorPicker.ItemsSource = ThemeManager.PrimaryNames.ToList();
        AccentColorPicker.ItemsSource = ThemeManager.AccentNames.ToList();
        BackgroundPalettePicker.ItemsSource = ThemeManager.BackgroundNames.ToList();
        LoadThemePreferences();
        ApiBaseUrlEntry.Text = Preferences.Get("Api_BaseUrl", OperatingSystem.IsAndroid()
            ? "http://10.0.2.2:5053/"
            : "http://localhost:5053/");
    }

    private void LoadThemePreferences()
    {
        _isInitializingThemeControls = true;
        PrimaryColorPicker.SelectedIndex = Math.Clamp(
            Preferences.Get("Theme_PrimaryIndex", 0), 0, ThemeManager.PrimaryNames.Count - 1);
        AccentColorPicker.SelectedIndex = Math.Clamp(
            Preferences.Get("Theme_AccentIndex", 0), 0, ThemeManager.AccentNames.Count - 1);
        BackgroundPalettePicker.SelectedIndex = Math.Clamp(
            Preferences.Get("Theme_BackgroundIndex", 0), 0, ThemeManager.BackgroundNames.Count - 1);
        DarkModeEnabled = Preferences.Get("Theme_DarkMode", true);
        DarkModeSwitch.IsToggled = DarkModeEnabled;
        _isInitializingThemeControls = false;
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            using var scope = App.Services!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = await db.Sensors.AsNoTracking().ToListAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var existing in _settings)
                    existing.PropertyChanged -= OnSettingPropertyChanged;
                _settings.Clear();
                foreach (var sensor in sensors)
                {
                    var setting = new SensorSetting
                    {
                        Id = sensor.Id,
                        Name = sensor.Name,
                        Model = sensor.Model ?? sensor.Type,
                        Type = sensor.Type,
                        Enabled = sensor.Enabled == 1,
                        MinThreshold = sensor.MinThreshold,
                        MaxThreshold = sensor.MaxThreshold
                    };
                    setting.ResetChanges();
                    setting.PropertyChanged += OnSettingPropertyChanged;
                    _settings.Add(setting);
                }
                UpdateSaveButtonState();
                _hasLoaded = true;
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load settings:\n\n{ex.Message}", "OK");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void UpdateSaveButtonState()
    {
        _hasUnsavedChanges = _settings.Any(s => s.HasChanges);
        SaveButton.IsEnabled = _hasUnsavedChanges;
        SaveButton.Text = _hasUnsavedChanges ? "Save changes - unsaved" : "Save changes";
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SensorSetting.HasChanges))
            UpdateSaveButtonState();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!_hasUnsavedChanges)
            return;

        SaveButton.IsEnabled = false;
        SaveButton.Text = "Saving...";

        try
        {
            using var scope = App.Services!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();

            foreach (var setting in _settings.Where(s => s.HasChanges))
            {
                var sensor = await db.Sensors.FindAsync(setting.Id);
                if (sensor == null) continue;

                sensor.Name = setting.Name;
                sensor.Enabled = setting.Enabled ? 1 : 0;
                sensor.MinThreshold = setting.MinThreshold;
                sensor.MaxThreshold = setting.MaxThreshold;
                setting.ResetChanges();
            }

            await db.SaveChangesAsync();
            UpdateSaveButtonState();
            SaveButton.Text = "Changes saved";
        }
        catch (Exception ex)
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "Save changes";
            await DisplayAlertAsync("Error", $"Failed to save settings:\n\n{ex.Message}", "OK");
        }
    }

    private void OnPrimaryColorChanged(object? sender, EventArgs e)
    {
        if (_isInitializingThemeControls || PrimaryColorPicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_PrimaryIndex", PrimaryColorPicker.SelectedIndex);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnAccentColorChanged(object? sender, EventArgs e)
    {
        if (_isInitializingThemeControls || AccentColorPicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_AccentIndex", AccentColorPicker.SelectedIndex);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnBackgroundPaletteChanged(object? sender, EventArgs e)
    {
        if (_isInitializingThemeControls || BackgroundPalettePicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_BackgroundIndex", BackgroundPalettePicker.SelectedIndex);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnDarkModeToggled(object? sender, ToggledEventArgs e)
    {
        if (_isInitializingThemeControls) return;
        DarkModeEnabled = e.Value;
        Preferences.Set("Theme_DarkMode", e.Value);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnResetPrimaryColor(object? sender, EventArgs e) => PrimaryColorPicker.SelectedIndex = 0;
    private void OnResetAccentColor(object? sender, EventArgs e) => AccentColorPicker.SelectedIndex = 0;
    private void OnResetBackgroundColor(object? sender, EventArgs e) => BackgroundPalettePicker.SelectedIndex = 0;

    private void OnUseRecommendedClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SensorSetting setting })
            setting.ApplyRecommended();
    }

    private void OnClearThresholdsClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SensorSetting setting })
            setting.ClearThresholds();
    }

    private async void OnSaveApiAddress(object? sender, EventArgs e)
    {
        var value = ApiBaseUrlEntry.Text?.Trim() ?? "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await DisplayAlertAsync("Invalid address", "Enter a complete HTTP or HTTPS address, including the port.", "OK");
            return;
        }

        value = value.EndsWith('/') ? value : value + "/";
        ApiBaseUrlEntry.Text = value;
        Preferences.Set("Api_BaseUrl", value);
        await DisplayAlertAsync("Address saved", "Reports will use this sensor API address for new connection tests.", "OK");
    }

    private void UpdateColorPreviews()
    {
        PrimaryColorPreview.BackgroundColor = GetResourceColor("Primary", Colors.Green);
        AccentColorPreview.BackgroundColor = GetResourceColor("Secondary", Colors.Blue);
        BackgroundColorPreview.BackgroundColor = GetResourceColor("Surface", Colors.White);
    }

    private static Color GetResourceColor(string key, Color fallback)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;

    private async void OnResetAllClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Reset All Settings",
            "Reset sensor settings and appearance to the unified GLORIOUS SYSTEM defaults?",
            "Reset",
            "Cancel");

        if (!confirm) return;

        PrimaryColorPicker.SelectedIndex = 0;
        AccentColorPicker.SelectedIndex = 0;
        BackgroundPalettePicker.SelectedIndex = 0;
        DarkModeSwitch.IsToggled = true;

        Preferences.Set("Theme_PrimaryIndex", 0);
        Preferences.Set("Theme_AccentIndex", 0);
        Preferences.Set("Theme_BackgroundIndex", 0);
        Preferences.Set("Theme_DarkMode", true);
        Preferences.Remove("Api_BaseUrl");
        ApiBaseUrlEntry.Text = OperatingSystem.IsAndroid() ? "http://10.0.2.2:5053/" : "http://localhost:5053/";

        ThemeManager.Apply();
        UpdateColorPreviews();
        await LoadAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateColorPreviews();
        if (!_hasLoaded)
            Dispatcher.Dispatch(() => _ = LoadAsync());
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
