using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public partial class AppSidebar : ContentView
{
    bool _isNavigating;

    public static readonly BindableProperty CurrentRouteProperty =
        BindableProperty.Create(nameof(CurrentRoute), typeof(string), typeof(AppSidebar), "dashboard", propertyChanged: OnCurrentRouteChanged);

    public string CurrentRoute
    {
        get => (string)GetValue(CurrentRouteProperty);
        set => SetValue(CurrentRouteProperty, value);
    }

    public AppSidebar()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSelection(CurrentRoute);
    }

    static void OnCurrentRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AppSidebar sidebar && sidebar.IsLoaded)
            sidebar.UpdateSelection((string)newValue);
    }

    void UpdateSelection(string route)
    {
        var active = Application.Current?.Resources["PrimaryContainer"] as Color;
        var inactive = Application.Current?.Resources["SurfaceBright"] as Color;
        var primary = Application.Current?.Resources["Primary"] as Color;
        var muted = Application.Current?.Resources["OnSurfaceVariant"] as Color;

        SetItem(OverviewItem, OverviewIcon, route == "dashboard", active, inactive);
        SetItem(ScannerItem, ScannerIcon, route == "webcam", active, inactive);
        SetItem(AnalyticsItem, AnalyticsIcon, route == "reports", active, inactive);
        SetItem(SettingsItem, SettingsIcon, route == "settings", active, inactive);
    }

    static void SetItem(Border item, Image icon, bool selected, Color? active, Color? inactive)
    {
        item.BackgroundColor = selected ? active : inactive;
        icon.Opacity = selected ? 1.0 : 0.62;
    }

    async Task Navigate(string route)
    {
        if (_isNavigating || CurrentRoute == route || Shell.Current == null)
            return;

        _isNavigating = true;
        try
        {
            CurrentRoute = route;
            UpdateSelection(route);
            await Shell.Current.GoToAsync($"//{route}", false);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    async void OnOverviewTapped(object? sender, TappedEventArgs e) => await Navigate("dashboard");
    async void OnScannerTapped(object? sender, TappedEventArgs e) => await Navigate("webcam");
    async void OnAnalyticsTapped(object? sender, TappedEventArgs e) => await Navigate("reports");
    async void OnSettingsTapped(object? sender, TappedEventArgs e) => await Navigate("settings");

    async void OnLogoutClicked(object? sender, EventArgs e)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync("Sign Out", "Are you sure you want to sign out?", "Yes", "Cancel");
        if (confirm)
            App.Logout();
    }
}
