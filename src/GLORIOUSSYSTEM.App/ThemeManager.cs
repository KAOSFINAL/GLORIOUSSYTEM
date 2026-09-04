using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the GLORIOUSSYSTEM visual theme.
/// The runtime palette intentionally matches the GLORIOUSSYSTEMATIC/Figma design.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 7;

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
        const string primary = "#F59E0B";
        const string primaryContainer = "#3A2410";
        const string secondary = "#F97316";
        const string secondaryContainer = "#35170F";
        const string tertiary = "#FCD34D";
        const string tertiaryContainer = "#3B2A0A";
        const string error = "#F97316";
        const string errorContainer = "#3A1710";
        const string surface = "#160A0C";
        const string surfaceDim = "#100607";
        const string surfaceBright = "#2A1318";
        const string surfaceContainer = "#1F0E12";
        const string surfaceContainerHigh = "#211014";
        const string surfaceContainerHighest = "#2A1318";
        const string onSurface = "#FDF0E8";
        const string onSurfaceVariant = "#7A4A55";
        const string outline = "#6B3742";
        const string outlineVariant = "#3A1B22";
        const string shadow = "#080304";

        app.UserAppTheme = AppTheme.Dark;

        Set(resources, "Primary", primary);
        Set(resources, "PrimaryDark", primary);
        Set(resources, "PrimaryContainer", primaryContainer);
        Set(resources, "PrimaryContainerDark", primaryContainer);
        Set(resources, "OnPrimary", "#1A0A00");
        Set(resources, "OnPrimaryDark", "#1A0A00");
        Set(resources, "OnPrimaryContainer", "#FDE68A");
        Set(resources, "OnPrimaryContainerDark", "#FDE68A");

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryDark", secondary);
        Set(resources, "SecondaryContainer", secondaryContainer);
        Set(resources, "SecondaryContainerDark", secondaryContainer);
        Set(resources, "OnSecondary", "#1A0A00");
        Set(resources, "OnSecondaryDark", "#1A0A00");
        Set(resources, "OnSecondaryContainer", "#FED7AA");
        Set(resources, "OnSecondaryContainerDark", "#FED7AA");

        Set(resources, "Tertiary", tertiary);
        Set(resources, "TertiaryDark", tertiary);
        Set(resources, "TertiaryContainer", tertiaryContainer);
        Set(resources, "TertiaryContainerDark", tertiaryContainer);
        Set(resources, "OnTertiary", "#1A0A00");
        Set(resources, "OnTertiaryDark", "#1A0A00");
        Set(resources, "OnTertiaryContainer", "#FEF3C7");
        Set(resources, "OnTertiaryContainerDark", "#FEF3C7");

        Set(resources, "Error", error);
        Set(resources, "ErrorDark", error);
        Set(resources, "ErrorContainer", errorContainer);
        Set(resources, "ErrorContainerDark", errorContainer);
        Set(resources, "OnError", "#1A0500");
        Set(resources, "OnErrorDark", "#1A0500");
        Set(resources, "OnErrorContainer", "#FED7AA");
        Set(resources, "OnErrorContainerDark", "#FED7AA");

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
        Set(resources, "InversePrimary", "#B45309");

        // Semantic statuses use the same gold/orange language as the reference
        // instead of reintroducing the old green/blue status palette.
        Set(resources, "StatusOnline", primary);
        Set(resources, "StatusOffline", onSurfaceVariant);
        Set(resources, "StatusWarning", tertiary);
        Set(resources, "StatusCritical", secondary);
        Set(resources, "StatusUnknown", outline);
        Set(resources, "Success", primary);
        Set(resources, "Warning", tertiary);
        Set(resources, "Info", secondary);

        // Compatibility aliases used by older pages.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", secondary);
        Set(resources, "AccentAmber", primary);
        Set(resources, "AccentRed", secondary);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", "#B98A94");
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
        SetBrush(resources, "AccentBlueBrush", secondary);
        SetBrush(resources, "TextPrimaryBrush", onSurface);
        SetBrush(resources, "TextSecondaryBrush", "#B98A94");
        SetBrush(resources, "TextMutedBrush", onSurfaceVariant);
        SetBrush(resources, "StatusOnlineBrush", primary);
        SetBrush(resources, "StatusOfflineBrush", onSurfaceVariant);
        SetBrush(resources, "StatusWarningBrush", tertiary);
        SetBrush(resources, "StatusCriticalBrush", secondary);

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
        Set(resources, "AppSuccess", primary);
        Set(resources, "AppWarning", tertiary);
        Set(resources, "AppError", secondary);
        Set(resources, "AppInfo", secondary);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Set(ResourceDictionary resources, string key, string value)
        => resources[key] = Color.FromArgb(value);

    private static void SetBrush(ResourceDictionary resources, string key, string value)
        => resources[key] = new SolidColorBrush(Color.FromArgb(value));
}