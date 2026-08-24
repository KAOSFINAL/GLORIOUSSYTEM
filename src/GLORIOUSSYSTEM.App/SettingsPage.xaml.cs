using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public class NotZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
            return index != 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SensorSetting : INotifyPropertyChanged
{
    int _id;
    string _name = "";
    string _model = "";
    string _type = "";
    bool _enabled;
    double? _minThreshold;
    double? _maxThreshold;
    bool _hasChanges = false;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); MarkChanged(); }
    }

    public string Model
    {
        get => _model;
        set { _model = value; OnPropertyChanged(); }
    }

    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); MarkChanged(); OnPropertyChanged(nameof(ThresholdStatusText)); OnPropertyChanged(nameof(ThresholdStatusColor)); OnPropertyChanged(nameof(ThresholdStatusTextColor)); }
    }

    public double? MinThreshold
    {
        get => _minThreshold;
        set { _minThreshold = value; OnPropertyChanged(); MarkChanged(); OnPropertyChanged(nameof(HasThresholds)); OnPropertyChanged(nameof(ThresholdStatusText)); OnPropertyChanged(nameof(ThresholdStatusColor)); OnPropertyChanged(nameof(ThresholdStatusTextColor)); }
    }

    public double? MaxThreshold
    {
        get => _maxThreshold;
        set { _maxThreshold = value; OnPropertyChanged(); MarkChanged(); OnPropertyChanged(nameof(HasThresholds)); OnPropertyChanged(nameof(ThresholdStatusText)); OnPropertyChanged(nameof(ThresholdStatusColor)); OnPropertyChanged(nameof(ThresholdStatusTextColor)); }
    }

    public bool HasThresholds => _minThreshold.HasValue || _maxThreshold.HasValue;

    public bool HasChanges
    {
        get => _hasChanges;
        set { _hasChanges = value; OnPropertyChanged(); }
    }

    public string ThresholdStatusText
    {
        get
        {
            if (!Enabled) return "Disabled";
            if (!HasThresholds) return "No thresholds set";
            return "Thresholds active";
        }
    }

    public Color ThresholdStatusColor
    {
        get
        {
            if (!Enabled) return Color.FromArgb("#64748B"); // Gray
            if (!HasThresholds) return Color.FromArgb("#F59E0B"); // Amber
            return Color.FromArgb("#10B981"); // Green
        }
    }

    public Color ThresholdStatusTextColor
    {
        get
        {
            if (!Enabled) return Colors.White;
            if (!HasThresholds) return Color.FromArgb("#78350F");
            return Colors.White;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    void MarkChanged() => HasChanges = true;

    public void ResetChanges() => HasChanges = false;
}

public partial class SettingsPage : ContentPage, INotifyPropertyChanged
{
    ObservableCollection<SensorSetting> _settings = new();
    bool _hasUnsavedChanges = false;
    bool _darkModeEnabled = false;

    // Predefined color themes
    static readonly (string Name, Color LightPrimary, Color LightSecondary, Color DarkPrimary, Color DarkSecondary)[] _colorThemes =
    {
        ("Hydroponic Green (Default)", Color.FromArgb("#059669"), Color.FromArgb("#1D4ED8"), Color.FromArgb("#34D399"), Color.FromArgb("#60A5FA")),
        ("Ocean Blue", Color.FromArgb("#0284C7"), Color.FromArgb("#059669"), Color.FromArgb("#38BDF8"), Color.FromArgb("#34D399")),
        ("Sunset Orange", Color.FromArgb("#EA580C"), Color.FromArgb("#D97706"), Color.FromArgb("#FB923C"), Color.FromArgb("#FBBF24")),
        ("Deep Purple", Color.FromArgb("#7C3AED"), Color.FromArgb("#6366F1"), Color.FromArgb("#A78BFA"), Color.FromArgb("#A5B4FC")),
        ("Rose Pink", Color.FromArgb("#E11D48"), Color.FromArgb("#DB2777"), Color.FromArgb("#F472B6"), Color.FromArgb("#F9A8D4")),
        ("Teal", Color.FromArgb("#0D9488"), Color.FromArgb("#06B6D4"), Color.FromArgb("#2DD4BF"), Color.FromArgb("#67E8F9")),
        ("Indigo", Color.FromArgb("#4F46E5"), Color.FromArgb("#6366F1"), Color.FromArgb("#818CF8"), Color.FromArgb("#A5B4FC")),
        ("Emerald", Color.FromArgb("#059669"), Color.FromArgb("#10B981"), Color.FromArgb("#34D399"), Color.FromArgb("#6EE7B7")),
    };

    static readonly (string Name, Color LightSecondary, Color DarkSecondary)[] _accentThemes =
    {
        ("Water Blue (Default)", Color.FromArgb("#1D4ED8"), Color.FromArgb("#60A5FA")),
        ("Growth Green", Color.FromArgb("#059669"), Color.FromArgb("#34D399")),
        ("Sun Amber", Color.FromArgb("#D97706"), Color.FromArgb("#FBBF24")),
        ("Alert Red", Color.FromArgb("#DC2626"), Color.FromArgb("#F87171")),
        ("Purple", Color.FromArgb("#7C3AED"), Color.FromArgb("#A78BFA")),
        ("Cyan", Color.FromArgb("#06B6D4"), Color.FromArgb("#67E8F9")),
        ("Lime", Color.FromArgb("#65A30D"), Color.FromArgb("#A3E635")),
        ("Pink", Color.FromArgb("#DB2777"), Color.FromArgb("#F472B6")),
    };

    static readonly (string Name, Color LightSurface, Color DarkSurface)[] _backgroundThemes =
    {
        ("Light Gray (Default)", Color.FromArgb("#FAFAFA"), Color.FromArgb("#1C1C1E")),
        ("Pure White", Color.FromArgb("#FFFFFF"), Color.FromArgb("#1C1C1E")),
        ("Warm White", Color.FromArgb("#FFF8F0"), Color.FromArgb("#1C1C1E")),
        ("Soft Green", Color.FromArgb("#ECFDF3"), Color.FromArgb("#064E3B")),
        ("Soft Blue", Color.FromArgb("#EFF6FF"), Color.FromArgb("#1E3A8A")),
        ("Dark Charcoal", Color.FromArgb("#1C1C1E"), Color.FromArgb("#141414")),
        ("Dark Navy", Color.FromArgb("#0F172A"), Color.FromArgb("#0F172A")),
        ("Custom Color", Color.FromArgb("#FAFAFA"), Color.FromArgb("#1C1C1E")), // Placeholder - will open color picker
    };

    public bool DarkModeEnabled
    {
        get => _darkModeEnabled;
        set { _darkModeEnabled = value; OnPropertyChanged(); }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
        SettingsList.ItemsSource = _settings;
        LoadThemePreferences();
        // Load sensors asynchronously to avoid blocking UI
        _ = LoadAsync();
    }

    void LoadThemePreferences()
    {
        // Load saved theme preferences
        var primaryIndex = Preferences.Get("Theme_PrimaryIndex", 0);
        var accentIndex = Preferences.Get("Theme_AccentIndex", 0);
        var backgroundIndex = Preferences.Get("Theme_BackgroundIndex", 0);
        var darkMode = Preferences.Get("Theme_DarkMode", false);

        if (primaryIndex >= 0 && primaryIndex < _colorThemes.Length)
        {
            PrimaryColorPicker.SelectedIndex = primaryIndex;
        }
        if (accentIndex >= 0 && accentIndex < _accentThemes.Length)
        {
            AccentColorPicker.SelectedIndex = accentIndex;
        }
        if (backgroundIndex >= 0 && backgroundIndex < _backgroundThemes.Length)
        {
            BackgroundColorPicker.SelectedIndex = backgroundIndex;
        }

        DarkModeEnabled = darkMode;
        DarkModeSwitch.IsToggled = darkMode;

        UpdateColorPreviews();
        ApplyTheme();
    }

    async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
            var sensors = await db.Sensors.ToListAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _settings.Clear();
                foreach (var s in sensors)
                {
                    _settings.Add(new SensorSetting
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Model = s.Model ?? s.Type,
                        Type = s.Type,
                        Enabled = s.Enabled == 1,
                        MinThreshold = s.MinThreshold,
                        MaxThreshold = s.MaxThreshold
                    });
                }

                // Reset change tracking after load
                foreach (var setting in _settings)
                {
                    setting.ResetChanges();
                }
                UpdateSaveButtonState();
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Error", $"Failed to load settings: {ex.Message}", "OK");
            });
        }
    }

    void UpdateSaveButtonState()
    {
        _hasUnsavedChanges = _settings.Any(s => s.HasChanges);
        SaveButton.IsEnabled = _hasUnsavedChanges;

        // Update button text to show change indicator
        SaveButton.Text = _hasUnsavedChanges ? "Save Changes ●" : "Save Changes";
    }

    void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SensorSetting.HasChanges))
        {
            UpdateSaveButtonState();
        }
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!_hasUnsavedChanges) return;

        SaveButton.IsEnabled = false;
        SaveButton.Text = "Saving...";

        try
        {
            using var scope = App.Services.CreateScope();
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

            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateSaveButtonState();
                SaveButton.Text = "Saved ✓";
            });

            // Show success feedback
            await Task.Delay(1000);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SaveButton.Text = "Save Changes";
            });

            // Animate success
            await AnimateSaveSuccess();
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                SaveButton.IsEnabled = true;
                SaveButton.Text = "Save Changes";
                await DisplayAlertAsync("Error", $"Failed to save settings: {ex.Message}", "OK");
            });
        }
    }

    async Task AnimateSaveSuccess()
    {
        await SaveButton.ScaleToAsync(1.05, 100, Easing.CubicOut);
        await SaveButton.ScaleToAsync(1.0, 100, Easing.CubicIn);
    }

    void OnPrimaryColorChanged(object? sender, EventArgs e)
    {
        if (PrimaryColorPicker.SelectedIndex >= 0)
        {
            Preferences.Set("Theme_PrimaryIndex", PrimaryColorPicker.SelectedIndex);
            UpdateColorPreviews();
            ApplyTheme();
        }
    }

    void OnAccentColorChanged(object? sender, EventArgs e)
    {
        if (AccentColorPicker.SelectedIndex >= 0)
        {
            Preferences.Set("Theme_AccentIndex", AccentColorPicker.SelectedIndex);
            UpdateColorPreviews();
            ApplyTheme();
        }
    }

    void OnResetPrimaryColor(object? sender, EventArgs e)
    {
        PrimaryColorPicker.SelectedIndex = 0;
        Preferences.Set("Theme_PrimaryIndex", 0);
        UpdateColorPreviews();
        ApplyTheme();
    }

    void OnResetAccentColor(object? sender, EventArgs e)
    {
        AccentColorPicker.SelectedIndex = 0;
        Preferences.Set("Theme_AccentIndex", 0);
        UpdateColorPreviews();
        ApplyTheme();
    }

    void OnBackgroundColorChanged(object? sender, EventArgs e)
    {
        if (BackgroundColorPicker.SelectedIndex >= 0)
        {
            Preferences.Set("Theme_BackgroundIndex", BackgroundColorPicker.SelectedIndex);
            UpdateColorPreviews();
            ApplyTheme();
        }
    }

    void OnResetBackgroundColor(object? sender, EventArgs e)
    {
        BackgroundColorPicker.SelectedIndex = 0;
        Preferences.Set("Theme_BackgroundIndex", 0);
        UpdateColorPreviews();
        ApplyTheme();
    }

    void OnDarkModeToggled(object? sender, ToggledEventArgs e)
    {
        DarkModeEnabled = e.Value;
        Preferences.Set("Theme_DarkMode", e.Value);
        ApplyTheme();
    }

    void UpdateColorPreviews()
    {
        if (PrimaryColorPicker.SelectedIndex >= 0 && PrimaryColorPicker.SelectedIndex < _colorThemes.Length)
        {
            var theme = _colorThemes[PrimaryColorPicker.SelectedIndex];
            PrimaryColorPreview.BackgroundColor = App.Current?.RequestedTheme == AppTheme.Dark ? theme.DarkPrimary : theme.LightPrimary;
        }

        if (AccentColorPicker.SelectedIndex >= 0 && AccentColorPicker.SelectedIndex < _accentThemes.Length)
        {
            var theme = _accentThemes[AccentColorPicker.SelectedIndex];
            AccentColorPreview.BackgroundColor = App.Current?.RequestedTheme == AppTheme.Dark ? theme.DarkSecondary : theme.LightSecondary;
        }

        if (BackgroundColorPicker.SelectedIndex >= 0 && BackgroundColorPicker.SelectedIndex < _backgroundThemes.Length)
        {
            var theme = _backgroundThemes[BackgroundColorPicker.SelectedIndex];
            BackgroundColorPreview.BackgroundColor = App.Current?.RequestedTheme == AppTheme.Dark ? theme.DarkSurface : theme.LightSurface;
        }
    }

    void ApplyTheme()
    {
        if (App.Current == null) return;

        // Apply dark mode
        var isDark = DarkModeEnabled;
        App.Current.UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;

        // Default light theme colors
        var surface = Color.FromArgb("#FAFAFA");
        var surfaceDim = Color.FromArgb("#E8E8E8");
        var surfaceBright = Color.FromArgb("#FFFFFF");
        var surfaceContainer = Color.FromArgb("#F5F5F5");
        var surfaceContainerHigh = Color.FromArgb("#EEEEEE");
        var surfaceContainerHighest = Color.FromArgb("#E0E0E0");
        var onSurface = Color.FromArgb("#1C1C1E");
        var onSurfaceVariant = Color.FromArgb("#49454F");
        var outline = Color.FromArgb("#79747E");
        var outlineVariant = Color.FromArgb("#CAC4D0");
        var shadow = Color.FromArgb("#000000");
        var scrim = Color.FromArgb("#000000");
        var inverseSurface = Color.FromArgb("#313033");
        var inverseOnSurface = Color.FromArgb("#F4EFF4");
        var inversePrimary = Color.FromArgb("#6EE7B7");

        // Default dark theme colors
        var surfaceDark = Color.FromArgb("#1C1C1E");
        var surfaceDimDark = Color.FromArgb("#141414");
        var surfaceBrightDark = Color.FromArgb("#2C2C2E");
        var surfaceContainerDark = Color.FromArgb("#242426");
        var surfaceContainerHighDark = Color.FromArgb("#2E2E30");
        var surfaceContainerHighestDark = Color.FromArgb("#3A3A3C");
        var onSurfaceDark = Color.FromArgb("#F4EFF4");
        var onSurfaceVariantDark = Color.FromArgb("#CAC4D0");
        var outlineDark = Color.FromArgb("#938F99");
        var outlineVariantDark = Color.FromArgb("#49454F");
        var shadowDark = Color.FromArgb("#000000");
        var scrimDark = Color.FromArgb("#000000");

        // Get resources once
        var resources = Application.Current?.Resources as ResourceDictionary;
        if (resources == null) return;

        // Apply background color theme - compute derived surface colors based on background selection
        Color bgLight = surface;
        Color bgDark = surfaceDark;
        if (BackgroundColorPicker.SelectedIndex >= 0 && BackgroundColorPicker.SelectedIndex < _backgroundThemes.Length)
        {
            var bgTheme = _backgroundThemes[BackgroundColorPicker.SelectedIndex];
            bgLight = bgTheme.LightSurface;
            bgDark = bgTheme.DarkSurface;
        }

        // Compute final surface colors based on theme and dark mode
        var finalSurface = isDark ? surfaceDark : bgLight;
        var finalSurfaceDim = isDark ? surfaceDimDark : bgLight.WithAlpha(0.9f);
        var finalSurfaceBright = isDark ? surfaceBrightDark : bgLight;
        var finalSurfaceContainer = isDark ? surfaceContainerDark : bgLight.WithAlpha(0.95f);
        var finalSurfaceContainerHigh = isDark ? surfaceContainerHighDark : bgLight.WithAlpha(0.9f);
        var finalSurfaceContainerHighest = isDark ? surfaceContainerHighestDark : bgLight.WithAlpha(0.85f);
        var finalOnSurface = isDark ? onSurfaceDark : onSurface;
        var finalOnSurfaceVariant = isDark ? onSurfaceVariantDark : onSurfaceVariant;
        var finalOutline = isDark ? outlineDark : outline;
        var finalOutlineVariant = isDark ? outlineVariantDark : outlineVariant;
        var finalShadow = isDark ? shadowDark : shadow;
        var finalScrim = isDark ? scrimDark : scrim;
        var finalInverseSurface = inverseSurface;
        var finalInverseOnSurface = inverseOnSurface;
        var finalInversePrimary = inversePrimary;

        // Surface colors
        resources["Surface"] = finalSurface;
        resources["SurfaceDim"] = finalSurfaceDim;
        resources["SurfaceBright"] = finalSurfaceBright;
        resources["SurfaceContainer"] = finalSurfaceContainer;
        resources["SurfaceContainerHigh"] = finalSurfaceContainerHigh;
        resources["SurfaceContainerHighest"] = finalSurfaceContainerHighest;
        resources["OnSurface"] = finalOnSurface;
        resources["OnSurfaceVariant"] = finalOnSurfaceVariant;
        resources["Outline"] = finalOutline;
        resources["OutlineVariant"] = finalOutlineVariant;
        resources["Shadow"] = finalShadow;
        resources["Scrim"] = finalScrim;
        resources["InverseSurface"] = finalInverseSurface;
        resources["InverseOnSurface"] = finalInverseOnSurface;
        resources["InversePrimary"] = finalInversePrimary;

        // Surface brushes
        resources["SurfaceBrush"] = new SolidColorBrush(isDark ? surfaceDark : surface);
        resources["SurfaceContainerBrush"] = new SolidColorBrush(isDark ? surfaceContainerDark : surfaceContainer);
        resources["SurfaceContainerHighBrush"] = new SolidColorBrush(isDark ? surfaceContainerHighDark : surfaceContainerHigh);
        resources["OnSurfaceBrush"] = new SolidColorBrush(isDark ? onSurfaceDark : onSurface);
        resources["OnSurfaceVariantBrush"] = new SolidColorBrush(isDark ? onSurfaceVariantDark : onSurfaceVariant);
        resources["OutlineBrush"] = new SolidColorBrush(isDark ? outlineDark : outline);
        resources["OutlineVariantBrush"] = new SolidColorBrush(isDark ? outlineVariantDark : outlineVariant);
        resources["ShadowBrush"] = new SolidColorBrush(isDark ? shadowDark : shadow);

        resources["SurfaceDarkBrush"] = new SolidColorBrush(surfaceDark);
        resources["SurfaceContainerDarkBrush"] = new SolidColorBrush(surfaceContainerDark);
        resources["OnSurfaceDarkBrush"] = new SolidColorBrush(onSurfaceDark);
        resources["OnSurfaceVariantDarkBrush"] = new SolidColorBrush(onSurfaceVariantDark);
        resources["OutlineDarkBrush"] = new SolidColorBrush(outlineDark);

        // Apply primary color theme
        if (PrimaryColorPicker.SelectedIndex >= 0 && PrimaryColorPicker.SelectedIndex < _colorThemes.Length)
        {
            var theme = _colorThemes[PrimaryColorPicker.SelectedIndex];

            resources["Primary"] = isDark ? theme.DarkPrimary : theme.LightPrimary;
            resources["PrimaryContainer"] = isDark ? theme.DarkPrimary.WithAlpha(0.2f) : theme.LightPrimary.WithAlpha(0.2f);
            resources["OnPrimary"] = Colors.White;
            resources["OnPrimaryContainer"] = isDark ? Colors.White : theme.LightPrimary.WithLuminosity(0.1f);

            resources["PrimaryDark"] = theme.DarkPrimary;
            resources["PrimaryContainerDark"] = theme.DarkPrimary.WithAlpha(0.2f);
            resources["OnPrimaryDark"] = Colors.Black;
            resources["OnPrimaryContainerDark"] = Colors.White;

            // Update brushes
            resources["PrimaryBrush"] = new SolidColorBrush(isDark ? theme.DarkPrimary : theme.LightPrimary);
            resources["PrimaryContainerBrush"] = new SolidColorBrush(isDark ? theme.DarkPrimary.WithAlpha(0.2f) : theme.LightPrimary.WithAlpha(0.2f));
            resources["OnPrimaryBrush"] = new SolidColorBrush(Colors.White);
            resources["PrimaryDarkBrush"] = new SolidColorBrush(theme.DarkPrimary);
            resources["PrimaryContainerDarkBrush"] = new SolidColorBrush(theme.DarkPrimary.WithAlpha(0.2f));
        }

        // Apply accent color theme
        if (AccentColorPicker.SelectedIndex >= 0 && AccentColorPicker.SelectedIndex < _accentThemes.Length)
        {
            var theme = _accentThemes[AccentColorPicker.SelectedIndex];

            resources["Secondary"] = isDark ? theme.DarkSecondary : theme.LightSecondary;
            resources["SecondaryContainer"] = isDark ? theme.DarkSecondary.WithAlpha(0.2f) : theme.LightSecondary.WithAlpha(0.2f);
            resources["OnSecondary"] = Colors.White;
            resources["OnSecondaryContainer"] = isDark ? Colors.White : theme.LightSecondary.WithLuminosity(0.1f);

            resources["SecondaryDark"] = theme.DarkSecondary;
            resources["SecondaryContainerDark"] = theme.DarkSecondary.WithAlpha(0.2f);
            resources["OnSecondaryDark"] = Colors.Black;
            resources["OnSecondaryContainerDark"] = Colors.White;

            // Update brushes
            resources["SecondaryBrush"] = new SolidColorBrush(isDark ? theme.DarkSecondary : theme.LightSecondary);
            resources["SecondaryContainerBrush"] = new SolidColorBrush(isDark ? theme.DarkSecondary.WithAlpha(0.2f) : theme.LightSecondary.WithAlpha(0.2f));
            resources["OnSecondaryBrush"] = new SolidColorBrush(Colors.White);
            resources["SecondaryDarkBrush"] = new SolidColorBrush(theme.DarkSecondary);
            resources["SecondaryContainerDarkBrush"] = new SolidColorBrush(theme.DarkSecondary.WithAlpha(0.2f));
        }

        // Apply error colors (consistent)
        resources["Error"] = isDark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626");
        resources["ErrorContainer"] = isDark ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#FEE2E2");
        resources["OnError"] = Colors.White;
        resources["OnErrorContainer"] = isDark ? Colors.White : Color.FromArgb("#7F1D1D");
        resources["ErrorDark"] = Color.FromArgb("#F87171");
        resources["ErrorContainerDark"] = Color.FromArgb("#7F1D1D");
        resources["OnErrorDark"] = Color.FromArgb("#7F1D1D");
        resources["OnErrorContainerDark"] = Color.FromArgb("#FEE2E2");
        resources["ErrorBrush"] = new SolidColorBrush(isDark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626"));
        resources["ErrorContainerBrush"] = new SolidColorBrush(isDark ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#FEE2E2"));
        resources["OnErrorBrush"] = new SolidColorBrush(Colors.White);
        resources["ErrorDarkBrush"] = new SolidColorBrush(Color.FromArgb("#F87171"));
        resources["ErrorContainerDarkBrush"] = new SolidColorBrush(Color.FromArgb("#7F1D1D"));

        UpdateColorPreviews();
    }

    async void OnResetAllClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Reset All Settings", "This will reset all sensor settings and theme colors to defaults. Continue?", "Yes", "No");
        if (!confirm) return;

        // Reset theme
        PrimaryColorPicker.SelectedIndex = 0;
        AccentColorPicker.SelectedIndex = 0;
        BackgroundColorPicker.SelectedIndex = 0;
        DarkModeEnabled = false;
        DarkModeSwitch.IsToggled = false;
        Preferences.Set("Theme_PrimaryIndex", 0);
        Preferences.Set("Theme_AccentIndex", 0);
        Preferences.Set("Theme_BackgroundIndex", 0);
        Preferences.Set("Theme_DarkMode", false);
        ApplyTheme();

        // Reload sensor settings from database
        _ = LoadAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Subscribe to property changes for change tracking
        foreach (var setting in _settings)
        {
            setting.PropertyChanged += OnSettingPropertyChanged;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        foreach (var setting in _settings)
        {
            setting.PropertyChanged -= OnSettingPropertyChanged;
        }
    }
}