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
		try
		{
			InitializeComponent();
			ConfigureServices();
			System.Diagnostics.Debug.WriteLine("App constructor completed successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"App constructor failed: {ex}");
			throw;
		}
	}

	private void ConfigureServices()
	{
		try
		{
			var services = new ServiceCollection();

			var config = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables()
				.Build();

			services.AddDbContext<HydroponicDbContext>(options =>
				options.UseSqlite(config.GetConnectionString("HydroponicDb")));

			Services = services.BuildServiceProvider();
			System.Diagnostics.Debug.WriteLine("ConfigureServices completed successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"ConfigureServices failed: {ex}");
			throw;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("CreateWindow called");

			// Check if user is logged in
			var isLoggedIn = Preferences.Get("IsLoggedIn", false);
			System.Diagnostics.Debug.WriteLine($"IsLoggedIn: {isLoggedIn}");

			Window window;
			if (isLoggedIn)
			{
				System.Diagnostics.Debug.WriteLine("Creating AppShell");
				window = new Window(new AppShell());
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("Creating LoginPage");
				window = new Window(new LoginPage());
			}

			System.Diagnostics.Debug.WriteLine("Window created successfully");
			return window;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"CreateWindow failed: {ex}");
			throw;
		}
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

		// Navigate to login page
		if (Current?.Windows.Count > 0)
		{
			var window = Current.Windows[0];
			window.Page = new LoginPage();
		}
	}
}