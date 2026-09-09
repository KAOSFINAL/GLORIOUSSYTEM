using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the GLORIOUSSYSTEM visual theme.
/// The palette uses neutral surfaces, one hydroponic-green action color, and a
/// dedicated solar-gold accent so operational states remain easy to scan.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 9;

    private static readonly string[] PrimaryOptions =
    {
        "#5EE0A0", "#66BFFF", "#A3E635", "#40D9D1",
        "#A78BFA", "#FB923C", "#FB7185", "#2DD4BF"
    };

    private static readonly string[] AccentOptions =
    {
        "#F4C95D", "#66BFFF", "#A78BFA", "#40D9D1",
        "#A3E635", "#FB923C", "#FB7185", "#2DD4BF"
    };

    public static event EventHandler? ThemeChanged;

    public static void Initialize()
    {
        // Reset older theme preferences so an old green/light theme cannot
        // override the reference palette on an existing installation.
        var version = Preferences.Get(ThemeVersionKey, 0);
        if (version < CurrentThemeVersion)
        {
            Preferences.Set("Theme_DarkMode", true);
            Preferences.Set("Theme_PrimaryIndex", 0);
            Preferences.Set("Theme_AccentIndex", 0);
            Preferences.Set("Theme_BackgroundIndex", 0);
            Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        }

        Apply();
    }

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var resources = app.Resources;
        var darkMode = Preferences.Get("Theme_DarkMode", true);
        var primaryIndex = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, PrimaryOptions.Length - 1);
        var accentIndex = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, AccentOptions.Length - 1);

        var primary = PrimaryOptions[primaryIndex];
        var secondary = AccentOptions[accentIndex];
        const string tertiary = "#66BFFF";
        const string error = "#FF7185";
        const string success = "#5EE0A0";
        const string warning = "#F4C95D";

        var primaryContainer = darkMode ? "#16382B" : "#D9F7E8";
        var secondaryContainer = darkMode ? "#382F16" : "#FFF2C7";
        var tertiaryContainer = darkMode ? "#142F46" : "#DDEEFF";
        var errorContainer = darkMode ? "#3C1C25" : "#FFE3E8";
        var surface = darkMode ? "#0A0F14" : "#F5F8FA";
        var surfaceDim = darkMode ? "#070B0F" : "#E9EFF3";
        var surfaceBright = darkMode ? "#17212B" : "#FFFFFF";
        var surfaceContainer = darkMode ? "#111922" : "#FFFFFF";
        var surfaceContainerHigh = darkMode ? "#17212B" : "#EEF3F6";
        var surfaceContainerHighest = darkMode ? "#1D2935" : "#E3EAEE";
        var onSurface = darkMode ? "#F4F7FA" : "#111820";
        var onSurfaceVariant = darkMode ? "#A8B5C2" : "#536271";
        var outline = darkMode ? "#405161" : "#91A0AD";
        var outlineVariant = darkMode ? "#263541" : "#D4DEE5";
        var shadow = darkMode ? "#030609" : "#1A2630";

        app.UserAppTheme = darkMode ? AppTheme.Dark : AppTheme.Light;

        Set(resources, "Primary", primary);
        Set(resources, "PrimaryDark", primary);
        Set(resources, "PrimaryContainer", primaryContainer);
        Set(resources, "PrimaryContainerDark", primaryContainer);
        Set(resources, "OnPrimary", "#07130E");
        Set(resources, "OnPrimaryDark", "#07130E");
        Set(resources, "OnPrimaryContainer", darkMode ? "#D9F7E8" : "#17382B");
        Set(resources, "OnPrimaryContainerDark", darkMode ? "#D9F7E8" : "#17382B");

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryDark", secondary);
        Set(resources, "SecondaryContainer", secondaryContainer);
        Set(resources, "SecondaryContainerDark", secondaryContainer);
        Set(resources, "OnSecondary", "#161105");
        Set(resources, "OnSecondaryDark", "#161105");
        Set(resources, "OnSecondaryContainer", darkMode ? "#FFF2C7" : "#382F16");
        Set(resources, "OnSecondaryContainerDark", darkMode ? "#FFF2C7" : "#382F16");

        Set(resources, "Tertiary", tertiary);
        Set(resources, "TertiaryDark", tertiary);
        Set(resources, "TertiaryContainer", tertiaryContainer);
        Set(resources, "TertiaryContainerDark", tertiaryContainer);
        Set(resources, "OnTertiary", "#07131E");
        Set(resources, "OnTertiaryDark", "#07131E");
        Set(resources, "OnTertiaryContainer", darkMode ? "#DDEEFF" : "#142F46");
        Set(resources, "OnTertiaryContainerDark", darkMode ? "#DDEEFF" : "#142F46");

        Set(resources, "Error", error);
        Set(resources, "ErrorDark", error);
        Set(resources, "ErrorContainer", errorContainer);
        Set(resources, "ErrorContainerDark", errorContainer);
        Set(resources, "OnError", "#23070E");
        Set(resources, "OnErrorDark", "#23070E");
        Set(resources, "OnErrorContainer", darkMode ? "#FFE3E8" : "#3C1C25");
        Set(resources, "OnErrorContainerDark", darkMode ? "#FFE3E8" : "#3C1C25");

        Set(resources, "Surface", surface);
        Set(resources, "SurfaceDark", surface);
        Set(resources, "SurfaceDim", surfaceDim);
        Set(resources, "SurfaceDimDark", surfaceDim);
        Set(resources, "SurfaceBright", surfaceBright);
        Set(resources, "SurfaceBrightDark", surfaceBright);
        Set(resources, "SurfaceContainer", surfaceContainer);
        Set(resources, "SurfaceContainerDark", surfaceContainer);
        Set(resources, "SurfaceContainerHigh", surfaceContainerHigh);
        Set(resources, "SurfaceContainerHighDark", surfaceContainerHigh);
        Set(resources, "SurfaceContainerHighest", surfaceContainerHighest);
        Set(resources, "SurfaceContainerHighestDark", surfaceContainerHighest);
        Set(resources, "OnSurface", onSurface);
        Set(resources, "OnSurfaceDark", onSurface);
        Set(resources, "OnSurfaceVariant", onSurfaceVariant);
        Set(resources, "OnSurfaceVariantDark", onSurfaceVariant);
        Set(resources, "Outline", outline);
        Set(resources, "OutlineDark", outline);
        Set(resources, "OutlineVariant", outlineVariant);
        Set(resources, "OutlineVariantDark", outlineVariant);
        Set(resources, "Shadow", shadow);
        Set(resources, "Scrim", shadow);
        Set(resources, "InverseSurface", onSurface);
        Set(resources, "InverseOnSurface", surface);
        Set(resources, "InversePrimary", primary);

        // Semantic colors do not change with appearance customization.
        Set(resources, "StatusOnline", success);
        Set(resources, "StatusOffline", onSurfaceVariant);
        Set(resources, "StatusWarning", warning);
        Set(resources, "StatusCritical", error);
        Set(resources, "StatusUnknown", outline);
        Set(resources, "Success", success);
        Set(resources, "Warning", warning);
        Set(resources, "Info", tertiary);

        // Compatibility aliases used by older pages.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", tertiary);
        Set(resources, "AccentAmber", secondary);
        Set(resources, "AccentRed", error);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", onSurfaceVariant);
        Set(resources, "TextMuted", onSurfaceVariant);
        Set(resources, "BgDark", surface);
        Set(resources, "CardDark", surfaceContainer);
        Set(resources, "BorderDark", outlineVariant);

        SetBrush(resources, "PrimaryBrush", primary);
        SetBrush(resources, "PrimaryContainerBrush", primaryContainer);
        SetBrush(resources, "PrimaryDarkBrush", primary);
        SetBrush(resources, "PrimaryContainerDarkBrush", primaryContainer);
        SetBrush(resources, "SecondaryBrush", secondary);
        SetBrush(resources, "SecondaryContainerBrush", secondaryContainer);
        SetBrush(resources, "SecondaryDarkBrush", secondary);
        SetBrush(resources, "SurfaceBrush", surface);
        SetBrush(resources, "SurfaceDarkBrush", surface);
        SetBrush(resources, "SurfaceContainerBrush", surfaceContainer);
        SetBrush(resources, "SurfaceContainerDarkBrush", surfaceContainer);
        SetBrush(resources, "SurfaceContainerHighBrush", surfaceContainerHigh);
        SetBrush(resources, "OnSurfaceBrush", onSurface);
        SetBrush(resources, "OnSurfaceDarkBrush", onSurface);
        SetBrush(resources, "OnSurfaceVariantBrush", onSurfaceVariant);
        SetBrush(resources, "OnSurfaceVariantDarkBrush", onSurfaceVariant);
        SetBrush(resources, "OutlineBrush", outline);
        SetBrush(resources, "OutlineDarkBrush", outline);
        SetBrush(resources, "OutlineVariantBrush", outlineVariant);
        SetBrush(resources, "ErrorBrush", error);
        SetBrush(resources, "ErrorDarkBrush", error);
        SetBrush(resources, "AccentGreenBrush", primary);
        SetBrush(resources, "AccentBlueBrush", tertiary);
        SetBrush(resources, "TextPrimaryBrush", onSurface);
        SetBrush(resources, "TextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "TextMutedBrush", onSurfaceVariant);
        SetBrush(resources, "StatusOnlineBrush", success);
        SetBrush(resources, "StatusOfflineBrush", onSurfaceVariant);
        SetBrush(resources, "StatusWarningBrush", warning);
        SetBrush(resources, "StatusCriticalBrush", error);

        Set(resources, "Background", surface);
        Set(resources, "CardBackground", surfaceContainer);
        Set(resources, "CardBackgroundElevated", surfaceBright);
        Set(resources, "Text", onSurface);
        Set(resources, "TextSecondarySemantic", onSurfaceVariant);
        Set(resources, "Border", outlineVariant);
        Set(resources, "Accent", secondary);
        Set(resources, "AppPrimary", primary);
        Set(resources, "AppAccent", secondary);
        Set(resources, "AppBackground", surface);
        Set(resources, "AppCard", surfaceContainer);
        Set(resources, "AppCardElevated", surfaceBright);
        Set(resources, "AppBorder", outlineVariant);
        Set(resources, "AppText", onSurface);
        Set(resources, "AppTextSecondary", onSurfaceVariant);
        Set(resources, "AppPrimarySoft", primaryContainer);
        Set(resources, "AppAccentSoft", secondaryContainer);
        Set(resources, "AppSuccess", success);
        Set(resources, "AppWarning", warning);
        Set(resources, "AppError", error);
        Set(resources, "AppInfo", tertiary);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Set(ResourceDictionary resources, string key, string value)
        => resources[key] = Color.FromArgb(value);

    private static void SetBrush(ResourceDictionary resources, string key, string value)
        => resources[key] = new SolidColorBrush(Color.FromArgb(value));
}
