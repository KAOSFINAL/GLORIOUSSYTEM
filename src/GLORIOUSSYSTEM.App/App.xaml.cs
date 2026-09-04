using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
using GLORIOUSSYSTEM.Data.Models;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public App()
    {
        InitializeComponent();
        ConfigureServices();

        // The Figma design uses the dark hydroponics palette. Only establish
        // these defaults when the user has not already chosen a theme.
        if (!Preferences.ContainsKey("Theme_DarkMode"))
            Preferences.Set("Theme_DarkMode", true);
        if (!Preferences.ContainsKey("Theme_PrimaryIndex"))
            Preferences.Set("Theme_PrimaryIndex", 2);
        if (!Preferences.ContainsKey("Theme_AccentIndex"))
            Preferences.Set("Theme_AccentIndex", 0);

        ThemeManager.Initialize();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        using var configStream = FileSystem.OpenAppPackageFileAsync("appsettings.json")
            .GetAwaiter()
            .GetResult();

        var config = new ConfigurationBuilder()
            .AddJsonStream(configStream)
            .AddEnvironmentVariables()
            .Build();

        var connStr = config.GetConnectionString("HydroponicDb");

        // The desktop configuration points to the development database on the
        // Windows machine. Android must use its own private app data folder.
        if (OperatingSystem.IsAndroid())
        {
            connStr = $"Data Source={Path.Combine(FileSystem.AppDataDirectory, "hydroponic.db")}";
        }

        services.AddDbContext<HydroponicDbContext>(options =>
            options.UseSqlite(connStr));

        Services = services.BuildServiceProvider();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var isLoggedIn = Preferences.Get("IsLoggedIn", false);

        Window window;
        if (isLoggedIn)
        {
            window = new Window(new AppShell());
        }
        else
        {
            window = new Window(new LoginPage());
        }

        return window;
    }

    public static void Logout()
    {
        Preferences.Remove("IsLoggedIn");
        Preferences.Remove("CurrentUserId");
        Preferences.Remove("CurrentUserEmail");
        Preferences.Remove("CurrentUserName");
        Preferences.Remove("RememberMe");
        Preferences.Remove("SavedEmail");

        if (Current?.Windows.Count > 0)
        {
            var window = Current.Windows[0];
            window.Page = new LoginPage();
        }
    }
}