using Microsoft.Maui.Controls;

namespace GLORIOUSSYSTEM.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        DashboardContent.Content = new MainPage();
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
