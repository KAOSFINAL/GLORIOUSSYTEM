using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public partial class AppShell : Shell
{
    bool _hasWarmedSecondaryPages;

    public AppShell()
    {
        InitializeComponent();

        // Render the first frame with only the initial destination. The other
        // cached pages are warmed just after the shell becomes visible, so the
        // splash logo is not held on screen while their XAML and model load.
        DashboardContent.Content = new MainPage();
        Loaded += OnShellLoaded;
    }

    void OnShellLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnShellLoaded;
        Dispatcher.Dispatch(() => _ = WarmSecondaryPagesAsync());
    }

    async Task WarmSecondaryPagesAsync()
    {
        if (_hasWarmedSecondaryPages)
            return;

        _hasWarmedSecondaryPages = true;
        await Task.Delay(120);
        EnsureContent("reports");
        await Task.Delay(60);
        EnsureContent("settings");
        await Task.Delay(60);
        EnsureContent("webcam");
    }

    void EnsureContent(string route)
    {
        switch (route)
        {
            case "dashboard":
                DashboardContent.Content ??= new MainPage();
                break;
            case "webcam":
                WebcamContent.Content ??= new WebcamPage();
                break;
            case "reports":
                ReportsContent.Content ??= new ReportsPage();
                break;
            case "settings":
                SettingsContent.Content ??= new SettingsPage();
                break;
        }
    }

    public void NavigateTo(string route)
    {
        EnsureContent(route);

        var destination = route switch
        {
            "dashboard" => DashboardItem,
            "webcam" => WebcamItem,
            "reports" => ReportsItem,
            "settings" => SettingsItem,
            _ => null
        };

        if (destination != null && CurrentItem != destination)
            CurrentItem = destination;
    }
}
