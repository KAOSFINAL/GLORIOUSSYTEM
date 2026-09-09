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
    public string Type { get => _type; set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(Category)); OnPropertyChanged(nameof(CategoryIcon)); OnPropertyChanged(nameof(CategoryColor)); OnPropertyChanged(nameof(CategoryContainerColor)); } }

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
    public Color CategoryColor => Category == "WATER" ? Color.FromArgb("#66BFFF") : Color.FromArgb("#5EE0A0");
    public Color CategoryContainerColor => Category == "WATER" ? Color.FromArgb("#142F46") : Color.FromArgb("#16382B");

    public string ThresholdStatusText => !Enabled ? "DISABLED" : !HasThresholds ? "SET LIMITS" : "ACTIVE";
    public Color ThresholdStatusColor => !Enabled ? Color.FromArgb("#263541") : !HasThresholds ? Color.FromArgb("#382F16") : Color.FromArgb("#16382B");
    public Color ThresholdStatusTextColor => !Enabled ? Color.FromArgb("#F4F7FA") : !HasThresholds ? Color.FromArgb("#FFF2C7") : Color.FromArgb("#D9F7E8");

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ResetChanges() => HasChanges = false;

    private void MarkChanged() => HasChanges = true;
    private void NotifyThresholdState()
    {
        OnPropertyChanged(nameof(HasThresholds));
        OnPropertyChanged(nameof(ThresholdStatusText));
        OnPropertyChanged(nameof(ThresholdStatusColor));
        OnPropertyChanged(nameof(ThresholdStatusTextColor));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SettingsPage : ContentPage, INotifyPropertyChanged
{
    private readonly ObservableCollection<SensorSetting> _settings = new();
    private bool _hasUnsavedChanges;
    private bool _darkModeEnabled;
    private bool _isLoading;

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
        LoadThemePreferences();
    }

    private void LoadThemePreferences()
    {
        PrimaryColorPicker.SelectedIndex = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, 7);
        AccentColorPicker.SelectedIndex = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, 7);
        DarkModeEnabled = Preferences.Get("Theme_DarkMode", true);
        DarkModeSwitch.IsToggled = DarkModeEnabled;
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
            await Task.Delay(350);
            SaveButton.Text = "Save changes";
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
        if (PrimaryColorPicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_PrimaryIndex", PrimaryColorPicker.SelectedIndex);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnAccentColorChanged(object? sender, EventArgs e)
    {
        if (AccentColorPicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_AccentIndex", AccentColorPicker.SelectedIndex);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnDarkModeToggled(object? sender, ToggledEventArgs e)
    {
        DarkModeEnabled = e.Value;
        Preferences.Set("Theme_DarkMode", e.Value);
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private void OnResetPrimaryColor(object? sender, EventArgs e) => PrimaryColorPicker.SelectedIndex = 0;
    private void OnResetAccentColor(object? sender, EventArgs e) => AccentColorPicker.SelectedIndex = 0;

    private void UpdateColorPreviews()
    {
        PrimaryColorPreview.BackgroundColor = GetResourceColor("Primary", Colors.Green);
        AccentColorPreview.BackgroundColor = GetResourceColor("Secondary", Colors.Blue);
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
        DarkModeSwitch.IsToggled = true;

        Preferences.Set("Theme_PrimaryIndex", 0);
        Preferences.Set("Theme_AccentIndex", 0);
        Preferences.Set("Theme_DarkMode", true);

        ThemeManager.Apply();
        UpdateColorPreviews();
        await LoadAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ThemeManager.Apply();
        UpdateColorPreviews();
        await Task.Yield();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        foreach (var setting in _settings)
            setting.PropertyChanged -= OnSettingPropertyChanged;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
