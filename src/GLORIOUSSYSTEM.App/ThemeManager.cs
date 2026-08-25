using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GLORIOUSSYSTEM.App;

/// <summary>
/// Single source of truth for the application's visual theme.
/// Pages should consume DynamicResource semantic keys instead of hard-coded
/// colors or AppThemeBinding color literals.
/// </summary>
public static class ThemeManager
{
    private const string ThemeVersionKey = "Theme_Version";
    private const int CurrentThemeVersion = 5;

    public static event EventHandler? ThemeChanged;

    public static void Initialize()
    {
        InitializeMissingPreferences();
        Apply();
    }

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        var resources = app.Resources;
        var dark = Preferences.Get("Theme_DarkMode", false);

        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;

        var primary = GetPrimary(dark);
        var secondary = GetSecondary(dark);
        var surface = GetBackground(dark);

        // Neutral surfaces are derived from the selected background.
        var surfaceContainer = dark
            ? Blend(surface, Colors.White, 0.08f)
            : Blend(surface, Colors.White, 0.70f);

        var surfaceContainerHigh = dark
            ? Blend(surface, Colors.White, 0.14f)
            : Blend(surface, Colors.White, 0.48f);

        var surfaceContainerHighest = dark
            ? Blend(surface, Colors.White, 0.20f)
            : Blend(surface, Colors.White, 0.28f);

        var surfaceBright = dark
            ? Blend(surface, Colors.White, 0.12f)
            : Colors.White;

        var surfaceDim = dark
            ? Blend(surface, Colors.Black, 0.12f)
            : Blend(surface, Colors.Black, 0.04f);

        var onSurface = dark
            ? Color.FromArgb("#F5F5F5")
            : Color.FromArgb("#18181B");

        var onSurfaceVariant = dark
            ? Color.FromArgb("#A1A1AA")
            : Color.FromArgb("#52525B");

        var outline = dark
            ? Color.FromArgb("#52525B")
            : Color.FromArgb("#D4D4D8");

        var outlineVariant = dark
            ? Color.FromArgb("#3F3F46")
            : Color.FromArgb("#E4E4E7");

        var primaryContainer = WithAlpha(primary, dark ? 0.22f : 0.12f);
        var secondaryContainer = WithAlpha(secondary, dark ? 0.20f : 0.10f);

        // Core semantic resources.
        Set(resources, "Primary", primary);
        Set(resources, "PrimaryDark", primary);
        Set(resources, "PrimaryContainer", primaryContainer);
        Set(resources, "PrimaryContainerDark", primaryContainer);
        Set(resources, "OnPrimary", Colors.White);
        Set(resources, "OnPrimaryDark", Colors.White);
        Set(resources, "OnPrimaryContainer", dark ? Color.FromArgb("#F4F4F5") : Darken(primary, 0.55f));
        Set(resources, "OnPrimaryContainerDark", dark ? Color.FromArgb("#F4F4F5") : Darken(primary, 0.55f));

        Set(resources, "Secondary", secondary);
        Set(resources, "SecondaryDark", secondary);
        Set(resources, "SecondaryContainer", secondaryContainer);
        Set(resources, "SecondaryContainerDark", secondaryContainer);
        Set(resources, "OnSecondary", Colors.White);
        Set(resources, "OnSecondaryDark", Colors.White);
        Set(resources, "OnSecondaryContainer", dark ? Color.FromArgb("#F4F4F5") : Darken(secondary, 0.55f));
        Set(resources, "OnSecondaryContainerDark", dark ? Color.FromArgb("#F4F4F5") : Darken(secondary, 0.55f));

        // Keep the warning/error palette semantic rather than tying it to the
        // user's chosen accent.
        Set(resources, "Tertiary", Color.FromArgb(dark ? "#FBBF24" : "#D97706"));
        Set(resources, "TertiaryDark", Color.FromArgb("#FBBF24"));
        Set(resources, "TertiaryContainer", Color.FromArgb(dark ? "#78350F" : "#FEF3C7"));
        Set(resources, "TertiaryContainerDark", Color.FromArgb("#78350F"));
        Set(resources, "OnTertiary", Colors.White);
        Set(resources, "OnTertiaryDark", Colors.White);
        Set(resources, "OnTertiaryContainer", Color.FromArgb(dark ? "#FEF3C7" : "#78350F"));
        Set(resources, "OnTertiaryContainerDark", Color.FromArgb("#FEF3C7"));

        Set(resources, "Surface", surface);
        Set(resources, "SurfaceDark", surface);
        Set(resources, "SurfaceDim", surfaceDim);
        Set(resources, "SurfaceDimDark", dark ? surfaceDim : Color.FromArgb("#141414"));
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

        var error = dark ? Color.FromArgb("#F87171") : Color.FromArgb("#DC2626");
        var errorContainer = dark ? Color.FromArgb("#7F1D1D") : Color.FromArgb("#FEE2E2");
        Set(resources, "Error", error);
        Set(resources, "ErrorDark", error);
        Set(resources, "ErrorContainer", errorContainer);
        Set(resources, "ErrorContainerDark", errorContainer);
        Set(resources, "OnError", Colors.White);
        Set(resources, "OnErrorDark", Colors.White);
        Set(resources, "OnErrorContainer", dark ? Colors.White : Color.FromArgb("#7F1D1D"));
        Set(resources, "OnErrorContainerDark", Colors.White);

        // Explicit application resources. Every modern page should use these
        // for accent/background/card styling.
        Set(resources, "AppPrimary", primary);
        Set(resources, "AppAccent", secondary);
        Set(resources, "AppBackground", surface);
        Set(resources, "AppCard", surfaceContainer);
        Set(resources, "AppCardElevated", surfaceBright);
        Set(resources, "AppPrimarySoft", primaryContainer);
        Set(resources, "AppAccentSoft", secondaryContainer);

        // Compatibility aliases. Older pages using these keys now follow the
        // selected primary/accent instead of creating a second green theme.
        Set(resources, "AccentGreen", primary);
        Set(resources, "AccentBlue", secondary);
        Set(resources, "TextPrimary", onSurface);
        Set(resources, "TextSecondary", onSurfaceVariant);
        Set(resources, "TextMuted", Color.FromArgb("#71717A"));
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
        SetBrush(resources, "TextSecondaryBrush", onSurfaceVariant);
        SetBrush(resources, "TextMutedBrush", Color.FromArgb("#71717A"));
        SetBrush(resources, "AppPrimaryBrush", primary);
        SetBrush(resources, "AppAccentBrush", secondary);
        SetBrush(resources, "AppBackgroundBrush", surface);
        SetBrush(resources, "AppCardBrush", surfaceContainer);
        SetBrush(resources, "AppPrimarySoftBrush", primaryContainer);
        SetBrush(resources, "AppAccentSoftBrush", secondaryContainer);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Color GetPrimary(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_PrimaryIndex", 0), 0, 7);
        var themes = new[]
        {
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#0284C7", Dark: "#38BDF8"),
            (Light: "#EA580C", Dark: "#FB923C"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#E11D48", Dark: "#FB7185"),
            (Light: "#0D9488", Dark: "#2DD4BF"),
            (Light: "#4F46E5", Dark: "#818CF8"),
            (Light: "#10B981", Dark: "#34D399")
        };

        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetSecondary(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_AccentIndex", 0), 0, 7);
        var themes = new[]
        {
            (Light: "#2563EB", Dark: "#60A5FA"),
            (Light: "#059669", Dark: "#34D399"),
            (Light: "#D97706", Dark: "#FBBF24"),
            (Light: "#DC2626", Dark: "#F87171"),
            (Light: "#7C3AED", Dark: "#A78BFA"),
            (Light: "#0891B2", Dark: "#67E8F9"),
            (Light: "#65A30D", Dark: "#A3E635"),
            (Light: "#DB2777", Dark: "#F472B6")
        };

        return Color.FromArgb(dark ? themes[index].Dark : themes[index].Light);
    }

    private static Color GetBackground(bool dark)
    {
        var index = Math.Clamp(Preferences.Get("Theme_BackgroundIndex", 0), 0, 7);

        if (dark)
        {
            return index switch
            {
                1 => Color.FromArgb("#151618"),
                2 => Color.FromArgb("#171719"),
                3 => Color.FromArgb("#15201B"),
                4 => Color.FromArgb("#151B22"),
                5 => Color.FromArgb("#18191B"),
                6 => Color.FromArgb("#171A20"),
                7 => Color.FromArgb("#16181C"),
                _ => Color.FromArgb("#18181B")
            };
        }

        return index switch
        {
            1 => Color.FromArgb("#FFFFFF"),
            2 => Color.FromArgb("#FCFAF7"),
            3 => Color.FromArgb("#F5FAF7"),
            4 => Color.FromArgb("#F6F9FD"),
            5 => Color.FromArgb("#F4F5F6"),
            6 => Color.FromArgb("#F3F6FA"),
            7 => Color.FromArgb("#F7F8FB"),
            _ => Color.FromArgb("#F6F7F8")
        };
    }

    private static void InitializeMissingPreferences()
    {
        if (!Preferences.ContainsKey("Theme_PrimaryIndex"))
            Preferences.Set("Theme_PrimaryIndex", 0);
        if (!Preferences.ContainsKey("Theme_AccentIndex"))
            Preferences.Set("Theme_AccentIndex", 0);
        if (!Preferences.ContainsKey("Theme_BackgroundIndex"))
            Preferences.Set("Theme_BackgroundIndex", 0);
        if (!Preferences.ContainsKey("Theme_DarkMode"))
            Preferences.Set("Theme_DarkMode", false);

        Preferences.Set(ThemeVersionKey, CurrentThemeVersion);
    }

    private static void Set(ResourceDictionary resources, string key, object value)
        => resources[key] = value;

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
        => resources[key] = new SolidColorBrush(color);

    private static Color WithAlpha(Color color, float alpha)
        => color.WithAlpha(alpha);

    private static Color Darken(Color color, float amount)
        => Blend(color, Colors.Black, Math.Clamp(amount, 0f, 1f));

    private static Color Blend(Color baseColor, Color overlay, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromRgba(
            baseColor.Red + (overlay.Red - baseColor.Red) * amount,
            baseColor.Green + (overlay.Green - baseColor.Green) * amount,
            baseColor.Blue + (overlay.Blue - baseColor.Blue) * amount,
            1f);
    }
}
