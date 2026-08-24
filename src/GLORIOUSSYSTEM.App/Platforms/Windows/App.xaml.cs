using Microsoft.UI.Xaml;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GLORIOUSSYSTEM.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		Debug.WriteLine("WinUI App constructor started");
		this.InitializeComponent();
		Debug.WriteLine("WinUI App constructor completed");
	}

	protected override MauiApp CreateMauiApp()
	{
		Debug.WriteLine("CreateMauiApp called");
		try
		{
			var app = MauiProgram.CreateMauiApp();
			Debug.WriteLine("CreateMauiApp succeeded");
			return app;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"CreateMauiApp failed: {ex}");
			throw;
		}
	}
}

