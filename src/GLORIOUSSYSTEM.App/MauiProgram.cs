using Microsoft.Extensions.Logging;

namespace GLORIOUSSYSTEM.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        try
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemiBold");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansMedium");
                });
#if DEBUG
            builder.Logging.AddDebug();
#endif
            var app = builder.Build();
            System.Diagnostics.Debug.WriteLine("MauiApp created successfully");
            return app;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MauiProgram.CreateMauiApp failed: {ex}");
            throw;
        }
    }
}