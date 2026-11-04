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
    LocalApiProcessManager? _localApiProcessManager;

    public App()
    {
        InitializeComponent();
        ConfigureServices();

        // Start with the low-glare command-center theme while preserving any
        // appearance choices the user has already made.
        if (!Preferences.ContainsKey("Theme_DarkMode"))
            Preferences.Set("Theme_DarkMode", true);
        if (!Preferences.ContainsKey("Theme_PrimaryIndex"))
            Preferences.Set("Theme_PrimaryIndex", 0);
        if (!Preferences.ContainsKey("Theme_AccentIndex"))
            Preferences.Set("Theme_AccentIndex", 0);
        if (!Preferences.ContainsKey("Theme_BackgroundIndex"))
            Preferences.Set("Theme_BackgroundIndex", 0);

        ThemeManager.Initialize();
        _ = _localApiProcessManager?.StartAsync();
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

        _localApiProcessManager = new LocalApiProcessManager(
            config["LocalApi:ProjectPath"],
            config["LocalApi:Url"]);

        var configuredConnectionString = config.GetConnectionString("HydroponicDb");
        var databasePath = HydroponicDatabasePath.Resolve(
            configuredConnectionString,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory);
        var connStr = $"Data Source={databasePath}";

        // The desktop configuration points to the development database on the
        // Windows machine. Android uses a private writable copy of that database.
        if (OperatingSystem.IsAndroid())
        {
            var androidDatabasePath = Path.Combine(FileSystem.AppDataDirectory, "hydroponic.db");
            InitializeAndroidDatabase(androidDatabasePath);
            connStr = $"Data Source={androidDatabasePath}";
        }
        else if (!File.Exists(databasePath))
        {
            InitializePackagedDatabase(databasePath);
        }

        services.AddDbContext<HydroponicDbContext>(options =>
            options.UseSqlite(connStr));

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HydroponicDbContext>();
        db.Database.Migrate();
        SensorHardwareCatalog.Reconcile(db);
        RemoveLegacyDemoAccount(db);
        db.SaveChanges();
    }

    private static void RemoveLegacyDemoAccount(HydroponicDbContext db)
    {
        // Older bundled databases included this publicly documented demo
        // account. Remove only that exact legacy record, never user accounts.
        var demoUser = db.Users.SingleOrDefault(user =>
            user.Email == "admin@glorious.com" &&
            user.Name == "Admin" &&
            user.Role == "Admin");

        if (demoUser != null)
            db.Users.Remove(demoUser);
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

    private static void InitializePackagedDatabase(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

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

        window.Destroying += (_, _) => _localApiProcessManager?.Dispose();

        return window;
    }

    public static async Task LogoutAsync()
    {
        var refreshToken = await SecureStorage.Default.GetAsync(ApiSensorService.RefreshTokenStorageKey);

        Preferences.Remove("IsLoggedIn");
        Preferences.Remove("CurrentUserId");
        Preferences.Remove("CurrentUserEmail");
        Preferences.Remove("CurrentUserName");
        Preferences.Remove("RememberMe");
        Preferences.Remove("SavedEmail");
        Preferences.Remove("SavedPassword");
        SecureStorage.Default.Remove(ApiSensorService.SessionTokenStorageKey);
        SecureStorage.Default.Remove(ApiSensorService.RefreshTokenStorageKey);

        if (Current?.Windows.Count > 0)
        {
            var window = Current.Windows[0];
            window.Page = new LoginPage();
        }

        _ = new ApiSensorService().LogoutAsync(refreshToken);
    }

    public static void Logout() => _ = LogoutAsync();
}
