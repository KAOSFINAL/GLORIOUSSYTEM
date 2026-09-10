using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

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

    public static readonly BindableProperty IsDesktopModeProperty =
        BindableProperty.Create(nameof(IsDesktopMode), typeof(bool), typeof(AppSidebar), false,
            propertyChanged: OnDesktopModeChanged);

    public bool IsDesktopMode
    {
        get => (bool)GetValue(IsDesktopModeProperty);
        set => SetValue(IsDesktopModeProperty, value);
    }

    public AppSidebar()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DesktopUserLabel.Text = Preferences.Get("CurrentUserName", "Administrator");
            UpdateDisplayMode();
            UpdateSelection(CurrentRoute);
        };
    }

    static void OnDesktopModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AppSidebar sidebar)
            sidebar.UpdateDisplayMode();
    }

    void UpdateDisplayMode()
    {
        DesktopNavigation.IsVisible = IsDesktopMode;
        MobileNavigation.IsVisible = !IsDesktopMode;
    }

    static void OnCurrentRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Parent XAML assigns CurrentRoute after this view is constructed. Do
        // not discard that early change: doing so left the default Dashboard
        // highlight visible in XAML Live Preview and occasionally on first load.
        if (bindable is AppSidebar sidebar)
            sidebar.UpdateSelection((string)newValue);
    }

    void UpdateSelection(string route)
    {
        SetItem(MobileOverviewItem, MobileOverviewIcon, route == "dashboard", "SurfaceContainerHigh");
        SetItem(MobileScannerItem, MobileScannerIcon, route == "webcam", "SurfaceContainerHigh");
        SetItem(MobileAnalyticsItem, MobileAnalyticsIcon, route == "reports", "SurfaceContainerHigh");
        SetItem(MobileSettingsItem, MobileSettingsIcon, route == "settings", "SurfaceContainerHigh");

        SetItem(DesktopOverviewItem, DesktopOverviewIcon, route == "dashboard", null);
        SetItem(DesktopScannerItem, DesktopScannerIcon, route == "webcam", null);
        SetItem(DesktopAnalyticsItem, DesktopAnalyticsIcon, route == "reports", null);
        SetItem(DesktopSettingsItem, DesktopSettingsIcon, route == "settings", null);
    }

    static void SetItem(Border item, MaterialIcon icon, bool selected, string? inactiveResource)
    {
        if (selected)
            item.SetDynamicResource(BackgroundColorProperty, "PrimaryContainer");
        else if (inactiveResource != null)
            item.SetDynamicResource(BackgroundColorProperty, inactiveResource);
        else
            item.BackgroundColor = Colors.Transparent;

        icon.SetDynamicResource(MaterialIcon.IconColorProperty, selected ? "OnPrimaryContainer" : "OnSurfaceVariant");
        icon.Opacity = selected ? 1.0 : 0.82;
    }

    void Navigate(string route)
    {
        if (_isNavigating || CurrentRoute == route || Shell.Current == null)
            return;

        _isNavigating = true;
        try
        {
            CurrentRoute = route;
            UpdateSelection(route);
            if (Shell.Current is AppShell appShell)
                appShell.NavigateTo(route);
            else
                _ = Shell.Current.GoToAsync($"//{route}", false);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    void OnOverviewTapped(object? sender, TappedEventArgs e) => Navigate("dashboard");
    void OnScannerTapped(object? sender, TappedEventArgs e) => Navigate("webcam");
    void OnAnalyticsTapped(object? sender, TappedEventArgs e) => Navigate("reports");
    void OnSettingsTapped(object? sender, TappedEventArgs e) => Navigate("settings");

    async void OnLogoutClicked(object? sender, EventArgs e)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync("Sign Out", "Are you sure you want to sign out?", "Yes", "Cancel");
        if (confirm)
            App.Logout();
    }
}
