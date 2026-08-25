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
    public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

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

    public string ThresholdStatusText => !Enabled ? "Disabled" : !HasThresholds ? "No thresholds set" : "Thresholds active";
    public Color ThresholdStatusColor => !Enabled ? Color.FromArgb("#64748B") : !HasThresholds ? Color.FromArgb("#F59E0B") : Color.FromArgb("#10B981");
    public Color ThresholdStatusTextColor => !Enabled || HasThresholds ? Colors.White : Color.FromArgb("#78350F");

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
        SettingsList.ItemsSource = _settings;
        LoadThemePreferences();
        _ = LoadAsync();
    }

    private void LoadThemePreferences()
    {
        PrimaryColorPicker.SelectedIndex = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, 7);
        AccentColorPicker.SelectedIndex = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, 7);
        BackgroundColorPicker.SelectedIndex = Math.Clamp(Preferences.Get("Theme_BackgroundIndex", 0), 0, 7);
        DarkModeEnabled = Preferences.Get("Theme_DarkMode", false);
        DarkModeSwitch.IsToggled = DarkModeEnabled;
        ThemeManager.Apply();
        UpdateColorPreviews();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = await db.Sensors.AsNoTracking().ToListAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
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
    }

    private void UpdateSaveButtonState()
    {
        _hasUnsavedChanges = _settings.Any(s => s.HasChanges);
        SaveButton.IsEnabled = _hasUnsavedChanges;
        SaveButton.Text = _hasUnsavedChanges ? "Save Changes ●" : "Save Changes";
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
            SaveButton.Text = "Saved ✓";
            await Task.Delay(800);
            SaveButton.Text = "Save Changes";
        }
        catch (Exception ex)
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "Save Changes";
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

    private void OnBackgroundColorChanged(object? sender, EventArgs e)
    {
        if (BackgroundColorPicker.SelectedIndex < 0) return;
        Preferences.Set("Theme_BackgroundIndex", BackgroundColorPicker.SelectedIndex);
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

    private void OnResetPrimaryColor(object? sender, EventArgs e)
    {
        PrimaryColorPicker.SelectedIndex = 0;
    }

    private void OnResetAccentColor(object? sender, EventArgs e)
    {
        AccentColorPicker.SelectedIndex = 0;
    }

    private void OnResetBackgroundColor(object? sender, EventArgs e)
    {
        BackgroundColorPicker.SelectedIndex = 0;
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
        BackgroundColorPicker.SelectedIndex = 0;
        DarkModeSwitch.IsToggled = false;

        Preferences.Set("Theme_PrimaryIndex", 0);
        Preferences.Set("Theme_AccentIndex", 0);
        Preferences.Set("Theme_BackgroundIndex", 0);
        Preferences.Set("Theme_DarkMode", false);

        ThemeManager.Apply();
        UpdateColorPreviews();
        await LoadAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ThemeManager.Apply();
        UpdateColorPreviews();
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
