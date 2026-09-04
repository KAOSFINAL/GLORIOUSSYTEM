using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
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
        // Windows machine. Android uses a private writable copy of that database.
        if (OperatingSystem.IsAndroid())
        {
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "hydroponic.db");
            InitializeAndroidDatabase(databasePath);
            connStr = $"Data Source={databasePath}";
        }

        services.AddDbContext<HydroponicDbContext>(options =>
            options.UseSqlite(connStr));

        Services = services.BuildServiceProvider();
    }

    private static void InitializeAndroidDatabase(string databasePath)
    {
        // An earlier build could have created an empty SQLite file on the phone.
        // Do not trust the file's existence alone; verify that it contains the
        // application schema before deciding to keep it.
        if (File.Exists(databasePath) && HasUsersTable(databasePath))
            return;

        if (File.Exists(databasePath))
            File.Delete(databasePath);

        using var source = FileSystem.OpenAppPackageFileAsync("database/hydroponic.db")
            .GetAwaiter()
            .GetResult();
        using var destination = File.Create(databasePath);
        source.CopyTo(destination);
    }

    private static bool HasUsersTable(string databasePath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='Users' LIMIT 1";
            return command.ExecuteScalar() != null;
        }
        catch
        {
            return false;
        }
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
        Preferences.Remove("SavedPassword");

        if (Current?.Windows.Count > 0)
        {
            var window = Current.Windows[0];
            window.Page = new LoginPage();
        }
    }
}